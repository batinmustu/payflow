# PayFlow umbrella Helm chart

One chart installs every PayFlow microservice (identity, payment, transaction,
reconciliation, reporting, notification, gateway) plus the shared ConfigMap and
Secret they all consume. Per-service overrides live in `values.yaml`.

This chart deliberately does **not** bundle Postgres / Kafka / Redis /
RabbitMQ / Jaeger / Seq — production deployments use managed services or
separate charts for those. The PayFlow services expect to reach those at
the hostnames listed under `shared.*` in `values.yaml`.

## Quick start

```bash
# 1. Replace every placeholder under `secrets:` with real values.
cp deploy/helm/payflow/values.yaml my-values.yaml
$EDITOR my-values.yaml

# 2. Render the manifest to inspect before applying.
helm template payflow ./deploy/helm/payflow -f my-values.yaml > rendered.yaml

# 3. Install (or upgrade) into the target namespace.
helm upgrade --install payflow ./deploy/helm/payflow \
  --namespace payflow --create-namespace \
  -f my-values.yaml
```

## What gets installed

| Resource | Count | Notes |
|----------|-------|-------|
| ConfigMap `payflow-shared-config` | 1 | Cross-cutting non-secret config. |
| Secret `payflow-shared-secrets` | 1 | JWT signing key + DB credentials. **Replace defaults.** |
| Deployment + Service `payflow-<name>` | one per enabled service | Default 2 replicas, rolling update with `maxUnavailable=0`. |
| Ingress `payflow-gateway` | 0 or 1 | Off by default; enable via `ingress.enabled=true`. |

Set `services.<name>.enabled=false` to skip any service — useful for partial
rollouts. The gateway is the only externally-routed pod; everything else
talks over the cluster DNS.

## Container images

Default image reference is `<registry>/<repository>:<tag>`. Override per
service with `services.<name>.image.repository` and (optionally) `.tag`.
The CI workflow at `.github/workflows/ci.yml` builds + pushes images to
GHCR — point `image.registry` at that registry path.

## Secrets

For real deployments do **not** put the contents of `secrets:` into a
plain values file. Use one of:

- [sealed-secrets](https://github.com/bitnami-labs/sealed-secrets)
- [external-secrets](https://external-secrets.io/)
- Vault / AWS Secrets Manager via the CSI driver

The chart requires every value under `secrets:` to be non-empty (`required`
template helper) so a missing key fails install loudly.
