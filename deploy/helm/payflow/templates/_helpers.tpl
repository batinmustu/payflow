{{/*
  PayFlow shared template helpers.
*/}}

{{/* Fully-qualified name for a per-service resource. */}}
{{- define "payflow.serviceFullname" -}}
{{- printf "payflow-%s" . | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{/* Common labels applied to every resource. */}}
{{- define "payflow.commonLabels" -}}
app.kubernetes.io/part-of: payflow
app.kubernetes.io/managed-by: {{ .Release.Service }}
helm.sh/chart: {{ printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end -}}

{{/*
  Per-service labels. Pass a tuple (rootContext, serviceName) e.g.
    {{ include "payflow.serviceLabels" (list $ "transaction") | nindent 4 }}
*/}}
{{- define "payflow.serviceLabels" -}}
{{- $root := index . 0 -}}
{{- $name := index . 1 -}}
app.kubernetes.io/name: {{ $name }}
{{ include "payflow.commonLabels" $root }}
{{- end -}}

{{/*
  Container image reference. Per-service `image.repository` is required;
  `tag` and `registry` fall back to top-level `image.*`. The tag is also
  overridable per service (services.<name>.image.tag).
*/}}
{{- define "payflow.image" -}}
{{- $root := index . 0 -}}
{{- $svc := index . 1 -}}
{{- $registry := default $root.Values.image.registry $svc.image.registry -}}
{{- $tag := default $root.Values.image.tag $svc.image.tag -}}
{{- printf "%s/%s:%s" $registry $svc.image.repository $tag -}}
{{- end -}}
