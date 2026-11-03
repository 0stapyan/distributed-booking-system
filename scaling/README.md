# Scaling README — Assignment 9

## Autoscaling signals

**ApiGateway → HPA on CPU.**
ApiGateway is a stateless HTTP router. Its load is proportional to request rate, which saturates CPU. A CPU-based HPA (`targetCPUUtilizationPercentage: 70`, min 2, max 6 replicas) is the right tool: CPU rises with traffic, and adding replicas directly reduces per-replica load.

**NotificationService → KEDA on RabbitMQ queue depth.**
NotificationService is a queue consumer. Consumers are I/O-bound: they spend most of their time waiting for the broker to deliver a message and for downstream ACKs. Even under a large backlog, CPU stays near zero. A CPU-HPA on a consumer sees nothing and never scales — we demonstrated this (the `cpu-wrong-signal.png` screenshot). KEDA's `ScaledObject` watches the actual queue length and scales the consumer to match the backlog, including scaling to zero replicas on an empty queue.

## Pod lifecycle — readiness in, graceful shutdown out

**Readiness gates pods in.**
The readiness probe (`/health/ready` on the management port 8081) checks real dependencies — Postgres for DB-backed services, RabbitMQ for the consumer. A pod is added to Service endpoints only when readiness returns 200. During a rolling update, new pods do not receive traffic until they pass readiness; during a scale-up, new replicas join after their dependencies are reachable. This prevents the "join-then-fail" window.

Liveness (`/health/live`) is process-only — no dependency checks — so a transient dependency blip does not trigger a restart storm across all replicas simultaneously.

**Graceful shutdown takes pods out.**
On SIGTERM: (1) the `preStop` sleep gives kube-proxy time to propagate endpoint removal before the app begins draining, (2) ASP.NET Core's Generic Host stops accepting new connections and finishes in-flight requests, (3) the consumer stops pulling new messages (`BasicCancelAsync`), finishes and acks the in-flight message, then closes the channel.

## Shutdown timing budget

| Setting | Value | Reason |
|---|---|---|
| `terminationGracePeriodSeconds` | 30 s | Total kubelet budget before SIGKILL |
| `lifecycle.preStopSleepSeconds` | 5 s | Endpoint propagation window |
| `HostOptions.ShutdownTimeout` | 15 s | App drain budget (30 − 5 = 25 s available; 15 s gives buffer before SIGKILL) |

The app has 25 seconds of drain time (grace − preStop). Setting `ShutdownTimeout` to 15 s keeps it well clear of the SIGKILL boundary, so a slow in-flight request does not cause the kubelet to kill the process mid-drain.

## Canary promote/rollback flow

GuestScoreService is called service-internally by BookingService through the Linkerd mesh. Because there is no public ingress involved, the caller's sidecar proxy can do the weighted split directly via a Linkerd-compatible `HTTPRoute`.

**Deploy phase.**
Two Helm releases run simultaneously: `guest-score-service` (stable, full replicas, latest image) and `guest-score-service-canary` (1 replica, no HPA, new image). An `HTTPRoute` on the stable Service parent splits traffic 80 % to stable, 20 % to canary. The canary bakes for 60 seconds under real traffic, then a health check on `/health/ready` confirms the pod is up.

**Promote.**
The stable Helm release is re-applied with the canary image tag. The `HTTPRoute` is deleted, routing all traffic back to the stable Service (which now runs the promoted image). The canary release is then removed. Result: zero-downtime promotion — at no point are there zero ready pods.

**Rollback.**
The `HTTPRoute` is deleted first (traffic returns 100 % to stable immediately). Then the canary Helm release is removed. Stable is never modified.

**Why a backward-incompatible contract change breaks the canary — and how expand-contract avoids it.**
During the bake window both releases run at the same time. If the canary ships a database schema change that drops a column the stable pods still read from, the still-running stable pods will start failing on every request that touches that column. The overlap is the danger.

The expand-contract pattern avoids this:
1. *Expand* — deploy a schema migration that adds the new column without removing the old one. Both stable and canary can work.
2. *Ship the canary* — canary uses the new column; stable still reads the old one.
3. *Promote* — once stable is updated everywhere.
4. *Contract* — only then drop the old column in a follow-up migration.

The same applies to any shared infrastructure: probe port changes, secret key renames, queue topology changes. Any backward-incompatible change must be split into a non-breaking expansion followed by a cleanup after full promotion.

## Rate limiting — why a shared counter is required under autoscaling

A per-instance in-memory counter multiplies the allowed rate by the replica count. With 3 gateway replicas and a 100 req/s limit, a client can hit 300 req/s total — the limit provides no real protection. Once the gateway autoscales, the effective limit becomes undefined.

A Redis-backed counter (using `RedisRateLimiting`) holds the sliding window state outside the process. All gateway instances share one counter, so the rate limit is enforced system-wide regardless of replica count. The client sees a consistent 429 as soon as it crosses the configured threshold, whether the request lands on replica 1 or replica 5.
