set -uo pipefail

NS="${NS:-team-06}"
CONSUMER="${CONSUMER:-notification-service}"
HTTP_SVC="${HTTP_SVC:-api-gateway}"

step() { printf '\n\033[1m[%s] %s\033[0m\n' "$(date -u +%H:%M:%S)" "$*"; }

step "baseline"
kubectl get pods -n "$NS" -l app="$CONSUMER" --no-headers 2>/dev/null
kubectl get pods -n "$NS" -l app="$HTTP_SVC" --no-headers 2>/dev/null
kubectl get hpa,scaledobject -n "$NS" 2>/dev/null

step "1/4 delete a BUSY CONSUMER pod (SIGTERM mid-message)"
POD=$(kubectl get pods -n "$NS" -l app="$CONSUMER" -o jsonpath='{.items[0].metadata.name}' 2>/dev/null)
if [ -n "$POD" ]; then
  echo "  deleting $POD"
  kubectl delete pod "$POD" -n "$NS" --wait=false
  sleep 25
  kubectl get pods -n "$NS" -l app="$CONSUMER" --no-headers 2>/dev/null
else
  echo "  no consumer pod running (KEDA may have it at 0 — is the queue backing up?)"
fi

step "2/4 delete a BUSY HTTP pod (SIGTERM mid-request)"
POD=$(kubectl get pods -n "$NS" -l app="$HTTP_SVC" -o jsonpath='{.items[0].metadata.name}' 2>/dev/null)
echo "  deleting $POD"
kubectl delete pod "$POD" -n "$NS" --wait=false
sleep 25
kubectl get pods -n "$NS" -l app="$HTTP_SVC" --no-headers 2>/dev/null

step "3/4 scale DOWN the HTTP service (routine scale-in)"
kubectl scale deploy "$HTTP_SVC" -n "$NS" --replicas=1
sleep 25
kubectl get pods -n "$NS" -l app="$HTTP_SVC" --no-headers 2>/dev/null

step "4/4 rollout restart (rolling update under load)"
kubectl rollout restart deploy "$HTTP_SVC" -n "$NS"
kubectl rollout status deploy "$HTTP_SVC" -n "$NS" --timeout=5m
kubectl rollout restart deploy "$CONSUMER" -n "$NS" 2>/dev/null || true

step "final state"
kubectl get pods -n "$NS" -l app="$HTTP_SVC" --no-headers 2>/dev/null
kubectl get pods -n "$NS" -l app="$CONSUMER" --no-headers 2>/dev/null
kubectl get hpa,scaledobject -n "$NS" 2>/dev/null

cat <<'EOF'

Next:
  1. Wait for the k6 job to finish, then read its PRODUCED count:
       kubectl logs job/k6-churn -n team-06 | tail -20
  2. In Grafana/Prometheus compare it against:
       sum(messages_consumed_total{queue="team-06.bookings.booking-created"})
     PRODUCED == consumed  ->  no-loss-under-churn.png
  3. Check the consumer drained rather than dropping:
       kubectl logs -n team-06 -l app=notification-service --previous | grep -i drain
EOF