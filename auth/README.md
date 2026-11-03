# Auth Gateway - TLS Shortcuts & Production Notes

## TLS shortcuts used in this cluster

### `RequireHttpsMetadata = false`
Set on both `AddOpenIdConnect` and `AddJwtBearer` in `BffAuthExtensions`.

**Why here:** The in-cluster Keycloak MetadataAddress (`http://keycloak.auth.svc.cluster.local/...`) is plain HTTP, so HTTPS metadata validation would always fail.

**Production alternative:** Use HTTPS for the MetadataAddress endpoint (backed by a valid TLS cert), and set `RequireHttpsMetadata = true`.

### `DangerousAcceptAnyServerCertificateValidator` on `BackchannelHttpHandler`
Set on both OIDC and JWT backchannel HTTP handlers.

**Why here:** The cluster uses a self-signed certificate. The backchannel (JWKS fetch, token endpoint calls) goes through HTTPS and would fail TLS validation without this override.

**Production alternative:** Install a trusted CA-signed certificate on the identity provider, or add the CA cert to the trust store of the gateway container, and remove this callback.