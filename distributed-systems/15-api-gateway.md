# API Gateway & Ingress

## What Is an API Gateway?

An API Gateway is a server that acts as the single entry point for all external client requests. It handles cross-cutting concerns before routing requests to backend services.

```
External Clients
       ↓
  API Gateway
  ┌──────────────────────────────────┐
  │ Auth  │ Rate Limit │ Routing     │
  │ TLS   │ Transform  │ Cache       │
  │ CORS  │ Logging    │ Load Balance│
  └──────────────────────────────────┘
       ↓           ↓           ↓
  Service A    Service B    Service C
```

---

## API Gateway vs Load Balancer vs Ingress vs Service Mesh

| | API Gateway | Load Balancer | Ingress (K8s) | Service Mesh |
|-|------------|---------------|--------------|--------------|
| **Layer** | L7 (HTTP/HTTPS) | L4/L7 | L7 | L7 |
| **Position** | External edge | External edge | Cluster edge | Internal (east-west) |
| **Auth** | ✓ (OAuth, API keys) | ✗ | Partial | Service identity |
| **Rate limiting** | ✓ | ✗ | Partial | ✗ |
| **Request transformation** | ✓ | ✗ | ✗ | ✗ |
| **Protocol translation** | ✓ (REST → gRPC) | ✗ | ✗ | ✗ |
| **Developer portal** | ✓ | ✗ | ✗ | ✗ |

Use an API Gateway at the edge, a Service Mesh internally, and Kubernetes Ingress when you need something in between with lighter overhead.

---

## Kong

Kong is the most widely deployed open-source API gateway. Built on NGINX + OpenResty (Lua).

### Architecture

```
Clients → Kong Proxy → Backend Services
              ↕
         Kong Admin API
              ↕
          PostgreSQL / Cassandra (config store)
```

In **DB-less mode**, Kong reads config from YAML/JSON files — ideal for Kubernetes (declarative config).

### Core Concepts

- **Service**: Represents an upstream service
- **Route**: URL patterns that map to a service
- **Plugin**: Middleware applied to routes/services/globally
- **Upstream**: Load balancing target (multiple targets = LB)
- **Consumer**: An API user (associated with credentials)

### Configuration Example

```yaml
# kong.yaml (declarative, DB-less)
_format_version: "3.0"

services:
- name: orders-api
  url: http://orders-service:8080
  routes:
  - name: orders-route
    paths:
    - /api/v1/orders
    methods:
    - GET
    - POST
  plugins:
  - name: rate-limiting
    config:
      minute: 100
      hour: 1000
      policy: local
  - name: jwt
    config:
      secret_is_base64: false

consumers:
- username: mobile-app
  jwt_secrets:
  - key: mobile-app-key
    secret: my-jwt-secret

plugins:
- name: cors
  config:
    origins:
    - https://app.example.com
    methods:
    - GET
    - POST
    headers:
    - Authorization
    - Content-Type
```

### Key Kong Plugins

| Plugin | Purpose |
|--------|---------|
| `jwt` / `oauth2` / `key-auth` | Authentication |
| `rate-limiting` | Requests per minute/hour/day per consumer |
| `ip-restriction` | Whitelist/blacklist IPs |
| `cors` | Cross-Origin Resource Sharing headers |
| `request-transformer` | Add/remove/rename headers, query params, body |
| `response-transformer` | Modify response headers and body |
| `proxy-cache` | Cache responses, reduce backend load |
| `zipkin` / `opentelemetry` | Distributed tracing |
| `prometheus` | Expose metrics |
| `bot-detection` | Block known bots |
| `grpc-gateway` | Translate REST to gRPC |

---

## NGINX as API Gateway / Ingress

NGINX is a high-performance reverse proxy. As an Ingress controller, it's the most commonly used in Kubernetes.

### NGINX Ingress Controller

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: myapp
  annotations:
    nginx.ingress.kubernetes.io/rewrite-target: /
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
    nginx.ingress.kubernetes.io/rate-limit: "100"
    nginx.ingress.kubernetes.io/rate-limit-burst-multiplier: "5"
    nginx.ingress.kubernetes.io/proxy-body-size: "10m"
    # CORS
    nginx.ingress.kubernetes.io/enable-cors: "true"
    nginx.ingress.kubernetes.io/cors-allow-origin: "https://app.example.com"
spec:
  ingressClassName: nginx
  tls:
  - hosts:
    - api.example.com
    secretName: api-tls
  rules:
  - host: api.example.com
    http:
      paths:
      - path: /api/v1
        pathType: Prefix
        backend:
          service:
            name: myapp
            port:
              number: 80
```

### NGINX for Custom Gateway (nginx.conf)

```nginx
upstream orders {
    least_conn;
    server orders-1:8080 weight=5;
    server orders-2:8080 weight=5;
    keepalive 64;
}

server {
    listen 443 ssl http2;
    server_name api.example.com;

    ssl_certificate /etc/ssl/cert.pem;
    ssl_certificate_key /etc/ssl/key.pem;
    ssl_protocols TLSv1.2 TLSv1.3;

    # Rate limiting (100 req/s per IP)
    limit_req_zone $binary_remote_addr zone=api:10m rate=100r/s;
    limit_req zone=api burst=200 nodelay;

    location /api/v1/orders {
        proxy_pass http://orders;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Request-ID $request_id;
        proxy_read_timeout 30s;
        proxy_send_timeout 30s;
    }
}
```

---

## Traefik

A cloud-native reverse proxy and load balancer with automatic service discovery.

```yaml
# traefik IngressRoute
apiVersion: traefik.containo.us/v1alpha1
kind: IngressRoute
metadata:
  name: myapp
spec:
  entryPoints:
  - websecure
  routes:
  - match: Host(`api.example.com`) && PathPrefix(`/api`)
    kind: Rule
    services:
    - name: myapp
      port: 80
    middlewares:
    - name: rate-limit
    - name: jwt-auth
  tls:
    certResolver: letsencrypt

---
apiVersion: traefik.containo.us/v1alpha1
kind: Middleware
metadata:
  name: rate-limit
spec:
  rateLimit:
    average: 100
    burst: 50
```

**Traefik advantages**:
- Automatic Let's Encrypt TLS
- Native Docker and Kubernetes service discovery (no config update needed when services appear)
- Dashboard out of the box

---

## Managed API Gateways (Cloud)

### Amazon API Gateway

- **REST API**: Traditional, feature-rich, per-request pricing
- **HTTP API**: Faster, cheaper, fewer features — good for Lambda and simple proxies
- **WebSocket API**: Full-duplex connections

Integration with: Lambda (serverless), Cognito (auth), WAF (security), CloudFront (CDN).

```
Client → API Gateway → Lambda function
                     → HTTP backend (VPC link)
                     → AWS service (S3, DynamoDB)
```

### Azure API Management

Full lifecycle API management: gateway + developer portal + analytics.

Features: Policies (transform, validate, cache, rate limit), subscriptions, product grouping, mock responses.

### Google Cloud API Gateway / Apigee

Apigee: Enterprise-grade, acquired by Google. Full API management platform with developer portal, monetization, and analytics.

---

## Authentication Patterns at the Gateway

### API Key Authentication

```
Client → "Authorization: ApiKey xyz123" → Gateway validates → Backend
```

- Simple to implement
- No expiry (risk if key is leaked)
- Good for server-to-server, webhooks, internal tools

### JWT Authentication

```
Client → "Authorization: Bearer <jwt>" → Gateway validates signature + expiry → Backend
```

- Stateless validation (no database lookup per request)
- Short-lived tokens + refresh tokens
- Standard for browser/mobile apps (OAuth 2.0 / OIDC)

### OAuth 2.0 / OIDC

```
User → Login → Identity Provider (Auth0, Keycloak, Okta) → Access Token → API Gateway
```

Gateway validates token via introspection endpoint or JWKS (JSON Web Key Set).

---

## Rate Limiting Strategies

| Strategy | Description | Use Case |
|----------|-------------|---------|
| **Fixed window** | N requests per window (e.g., 100/min) | Simple, predictable |
| **Sliding window** | N requests per rolling N seconds | Smoother, prevents edge-of-window bursts |
| **Token bucket** | Accumulate tokens; spend on requests | Allows bursts up to bucket size |
| **Leaky bucket** | Requests flow out at constant rate; overflow rejected | Smooth, consistent output rate |

### Rate Limit by Tier

```yaml
# Kong: different limits per consumer group
- name: rate-limiting
  config:
    minute: 60          # free tier
- name: rate-limiting
  consumer: premium-user
  config:
    minute: 1000        # premium tier
```

---

## Request/Response Transformation

API Gateways can transform requests without changing backend code:

```yaml
# Add headers before forwarding
- name: request-transformer
  config:
    add:
      headers:
      - X-Consumer-ID:$(consumer.id)
      - X-Request-Start:$(now)

# Remove internal headers from response
- name: response-transformer
  config:
    remove:
      headers:
      - X-Powered-By
      - Server
```

---

## Developer Portal

Managed API gateways (Kong Developer Portal, Apigee, AWS API Gateway with developer portal) provide:

- Interactive API documentation (OpenAPI/Swagger)
- API key self-service
- Usage analytics for consumers
- Onboarding and subscription management

---

## Production Checklist

- [ ] TLS termination at the gateway (HTTPS everywhere)
- [ ] Authentication on all external endpoints
- [ ] Rate limiting per consumer and per endpoint
- [ ] Request size limits configured
- [ ] CORS headers correctly configured for browser clients
- [ ] Remove server information headers (`Server`, `X-Powered-By`)
- [ ] Access logs with request IDs for correlation
- [ ] Health check endpoint excluded from auth/rate limiting
- [ ] Timeout set for upstream calls (prevent hanging connections)
- [ ] Gateway itself in HA (multiple replicas, PodDisruptionBudget)
- [ ] DDoS protection at the CDN/WAF layer above the gateway

---

## Next Steps

- [Service Mesh](./14-service-mesh.md) — internal traffic management (east-west)
- [Security](./18-security.md) — authentication and authorization in depth
- [Observability](./16-observability.md) — access logs and metrics from the gateway
