# Data Serialization & API Contracts

> **License quick-ref** — Protobuf: open-source (BSD-3). gRPC: Apache-2. Apache Avro: Apache-2. Apache Thrift: Apache-2. MessagePack: MIT. FlatBuffers: Apache-2. JSON Schema: free spec. OpenAPI: Apache-2. GraphQL spec: MIT. All tools listed here are open-source unless noted.

---

## Why Serialization Matters in Distributed Systems

Every byte that crosses a service boundary is serialized and deserialized. At scale, this becomes significant:

- **Performance**: JSON parsing of a 10KB payload at 50k req/s = 500MB/s of parsing work
- **Schema evolution**: Service A updated its API but Service B still sends the old format — does it break?
- **Type safety**: JSON has no schema; a field that becomes null silently corrupts downstream processing
- **Bandwidth**: A 10KB JSON payload may compress to 3KB with Protobuf — 70% less network traffic

---

## Serialization Formats Compared

| Format | Size | Speed | Schema | Human-readable | Best For |
|--------|------|-------|--------|---------------|---------|
| **JSON** | Large | Slow | Optional (JSON Schema) | ✓ | REST APIs, external APIs, config |
| **Protocol Buffers** | Small | Fast | Required (.proto) | ✗ | gRPC, internal services, Kafka |
| **Apache Avro** | Small | Fast | Required (.avsc) | ✗ | Kafka + Schema Registry |
| **Apache Thrift** | Small | Fast | Required (.thrift) | ✗ | Facebook legacy, some RPC systems |
| **MessagePack** | Medium | Fast | Optional | ✗ | Binary JSON alternative |
| **FlatBuffers** | Smallest | Fastest (zero-copy) | Required (.fbs) | ✗ | Games, embedded, extreme perf |
| **CBOR** | Medium | Medium | Optional | ✗ | IoT, IETF standards |
| **XML** | Largest | Slowest | Optional (XSD) | ✓ | Legacy SOAP, enterprise integrations |

---

## JSON

Ubiquitous, human-readable, universally supported. The right choice for external APIs and developer-facing interfaces.

### JSON Schema

```json
{
  "$schema": "https://json-schema.org/draft/2020-12",
  "title": "Order",
  "type": "object",
  "required": ["order_id", "customer_id", "items"],
  "properties": {
    "order_id": { "type": "string", "format": "uuid" },
    "customer_id": { "type": "string" },
    "items": {
      "type": "array",
      "minItems": 1,
      "items": {
        "type": "object",
        "required": ["product_id", "quantity"],
        "properties": {
          "product_id": { "type": "string" },
          "quantity": { "type": "integer", "minimum": 1 }
        }
      }
    },
    "total_cents": { "type": "integer" }
  }
}
```

### JSON Pitfalls in Distributed Systems

| Pitfall | Problem | Fix |
|---------|---------|-----|
| No strict types | `"amount": "99.99"` vs `"amount": 99.99` | JSON Schema validation at service boundary |
| Null vs absent | `{"name": null}` vs `{}` — semantically different in some systems | Explicit null handling in schema |
| 64-bit integers | JavaScript only handles 53-bit integers safely | Serialize large IDs as strings |
| Floats | IEEE 754 floating point in JSON | Use integers for money (cents); use strings for precision |
| Date format | No standard → ISO 8601 or Unix timestamp? | Agree on format in OpenAPI spec |

---

## Protocol Buffers (Protobuf)

Google's binary serialization format. Smaller and faster than JSON. Requires a schema `.proto` file.

### Defining a Schema

```protobuf
// order.proto
syntax = "proto3";

package payments.v1;

message Order {
  string order_id = 1;
  string customer_id = 2;
  repeated OrderItem items = 3;
  int64 total_cents = 4;
  OrderStatus status = 5;
  google.protobuf.Timestamp created_at = 6;
}

message OrderItem {
  string product_id = 1;
  int32 quantity = 2;
  int64 price_cents = 3;
}

enum OrderStatus {
  ORDER_STATUS_UNSPECIFIED = 0;
  ORDER_STATUS_PENDING = 1;
  ORDER_STATUS_PAID = 2;
  ORDER_STATUS_SHIPPED = 3;
}
```

### Generating Code

```bash
# Install protoc + language plugins
protoc --go_out=. --go-grpc_out=. order.proto       # Go
protoc --java_out=. order.proto                      # Java
protoc --python_out=. order.proto                    # Python
protoc --ts_out=. order.proto                        # TypeScript (protoc-gen-ts)
```

### Schema Evolution Rules

Protobuf uses **field numbers**, not field names, for encoding. Rules for backward-compatible changes:

| Change | Safe? | Notes |
|--------|-------|-------|
| Add new field | ✓ | Old readers ignore unknown fields; new readers get default value if absent |
| Remove a field | ✓ (with caution) | Must reserve the field number: `reserved 4;` |
| Rename a field | ✓ | Field number is what matters, not the name |
| Change field type | ✗ | Wire type mismatch causes decode errors |
| Change field number | ✗ | Breaks all existing serialized data |
| Add value to enum | ✓ | Old readers treat as `UNSPECIFIED` |

```protobuf
message Order {
  // Old fields that were removed — numbers reserved forever
  reserved 7, 8, 15;
  reserved "legacy_field";
}
```

---

## Apache Avro

Schema-based serialization with a focus on **schema evolution** and **Kafka integration**. Schemas stored in Schema Registry.

### Schema Definition

```json
{
  "type": "record",
  "name": "Order",
  "namespace": "com.myapp.orders",
  "fields": [
    {"name": "order_id", "type": "string"},
    {"name": "customer_id", "type": "string"},
    {
      "name": "status",
      "type": {
        "type": "enum",
        "name": "OrderStatus",
        "symbols": ["PENDING", "PAID", "SHIPPED"]
      }
    },
    {
      "name": "shipping_address",
      "type": ["null", "string"],  // nullable
      "default": null
    }
  ]
}
```

### Avro + Kafka Schema Registry

```python
from confluent_kafka import avro
from confluent_kafka.avro import AvroProducer

producer = AvroProducer(
    {'bootstrap.servers': 'kafka:9092',
     'schema.registry.url': 'http://schema-registry:8081'},
    default_value_schema=avro.load('order.avsc')
)

producer.produce(topic='orders', value={"order_id": "123", "status": "PENDING"})
```

Only the schema ID is embedded in the message (4 bytes), not the full schema. The consumer fetches the schema from the registry using the ID.

### Schema Compatibility Modes

| Mode | Allowed Changes |
|------|----------------|
| **BACKWARD** | New schema can read data written by old schema. Add field with default; delete field without default. |
| **FORWARD** | Old schema can read data written by new schema. Delete field with default; add field without default. |
| **FULL** | Both backward and forward compatible. |
| **NONE** | No checks. |

```bash
# Set compatibility on a topic's schema
curl -X PUT http://schema-registry:8081/config/orders-value \
  -d '{"compatibility": "BACKWARD"}'
```

---

## gRPC

gRPC is an RPC framework from Google that uses HTTP/2 and Protocol Buffers.

### Why gRPC Over REST?

| Feature | gRPC | REST/JSON |
|---------|------|-----------|
| Protocol | HTTP/2 (multiplexing, header compression) | HTTP/1.1 (or HTTP/2) |
| Serialization | Protobuf (binary, typed) | JSON (text, untyped) |
| Streaming | Bi-directional streaming native | Limited (SSE, WebSocket separate) |
| Code generation | Automatic client/server stubs | Manual or OpenAPI generator |
| Performance | 5–10x faster than REST/JSON | Baseline |
| Browser support | Limited (grpc-web proxy needed) | ✓ Native |
| Human debugging | Harder (binary) | Easy (curl) |

**Choose gRPC** for: internal service-to-service communication, high-throughput APIs, streaming use cases, polyglot services.

**Choose REST/JSON** for: external/public APIs, browser clients, simple CRUD.

### Service Definition

```protobuf
// payment_service.proto
service PaymentService {
  // Unary RPC
  rpc ChargeCard(ChargeRequest) returns (ChargeResponse);

  // Server streaming
  rpc StreamTransactions(TransactionFilter) returns (stream Transaction);

  // Client streaming
  rpc BulkCharge(stream ChargeRequest) returns (BulkChargeResponse);

  // Bi-directional streaming
  rpc ProcessPaymentStream(stream ChargeRequest) returns (stream ChargeResponse);
}

message ChargeRequest {
  string order_id = 1;
  int64 amount_cents = 2;
  string currency = 3;
  CardDetails card = 4;
}
```

### gRPC Server (Go)

```go
type PaymentServer struct {
    pb.UnimplementedPaymentServiceServer
}

func (s *PaymentServer) ChargeCard(ctx context.Context, req *pb.ChargeRequest) (*pb.ChargeResponse, error) {
    // implementation
    return &pb.ChargeResponse{Success: true, TransactionId: "txn-123"}, nil
}

func main() {
    lis, _ := net.Listen("tcp", ":50051")
    s := grpc.NewServer(
        grpc.UnaryInterceptor(loggingInterceptor),
    )
    pb.RegisterPaymentServiceServer(s, &PaymentServer{})
    s.Serve(lis)
}
```

### gRPC Error Handling

gRPC has structured status codes (not HTTP status codes):

| Status | Meaning |
|--------|---------|
| `OK` | Success |
| `INVALID_ARGUMENT` | Bad input (like HTTP 400) |
| `NOT_FOUND` | Resource doesn't exist (like HTTP 404) |
| `ALREADY_EXISTS` | Conflict (like HTTP 409) |
| `PERMISSION_DENIED` | AuthZ failure (like HTTP 403) |
| `UNAUTHENTICATED` | AuthN failure (like HTTP 401) |
| `RESOURCE_EXHAUSTED` | Rate limited (like HTTP 429) |
| `UNAVAILABLE` | Service down, retry (like HTTP 503) |
| `INTERNAL` | Server error (like HTTP 500) |
| `DEADLINE_EXCEEDED` | Timeout |

```go
return nil, status.Errorf(codes.InvalidArgument, "amount must be positive, got %d", req.AmountCents)
```

### gRPC Interceptors (Middleware)

```go
func loggingInterceptor(ctx context.Context, req interface{}, info *grpc.UnaryServerInfo, handler grpc.UnaryHandler) (interface{}, error) {
    start := time.Now()
    resp, err := handler(ctx, req)
    log.Printf("method=%s duration=%s error=%v", info.FullMethod, time.Since(start), err)
    return resp, err
}
```

---

## OpenAPI / Swagger — REST Contract

OpenAPI (formerly Swagger) is the standard for describing REST APIs. Enables:
- Auto-generated client SDKs
- Interactive documentation (Swagger UI)
- Request/response validation
- Contract testing

```yaml
# openapi.yaml
openapi: "3.1.0"
info:
  title: Payment API
  version: "1.0.0"

paths:
  /payments:
    post:
      operationId: createPayment
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: "#/components/schemas/PaymentRequest"
      responses:
        "200":
          content:
            application/json:
              schema:
                $ref: "#/components/schemas/PaymentResponse"
        "400":
          $ref: "#/components/responses/ValidationError"

components:
  schemas:
    PaymentRequest:
      type: object
      required: [order_id, amount_cents, currency]
      properties:
        order_id:
          type: string
          format: uuid
        amount_cents:
          type: integer
          minimum: 1
        currency:
          type: string
          pattern: "^[A-Z]{3}$"
```

---

## API Versioning Strategies

| Strategy | Example | Pros | Cons |
|----------|---------|------|------|
| **URL path versioning** | `/api/v1/orders` | Visible, simple caching | URL changes |
| **Query param** | `/orders?version=1` | No URL change | Easy to miss |
| **Header versioning** | `Accept: application/vnd.myapi.v1+json` | Clean URLs | Less visible |
| **No versioning (evolution)** | Backward-compatible changes only | Simple | Constrained by compatibility |

**Recommendation**: URL path versioning (`/v1/`, `/v2/`) for external APIs. No versioning (backward-compatible evolution) for internal gRPC services.

---

## Production Checklist

- [ ] Schema registry enforced for Kafka topics (Avro or Protobuf)
- [ ] Schema compatibility mode set (BACKWARD for most use cases)
- [ ] gRPC TLS enabled for all inter-service calls
- [ ] OpenAPI spec generated from code (not handwritten) — source of truth is the code
- [ ] Breaking API changes detected in CI (openapi-diff, buf breaking)
- [ ] Protobuf field numbers never reused (use `reserved`)
- [ ] Request validation at service boundary (reject invalid inputs early)
- [ ] Large binary payloads chunked or streamed (gRPC streaming)
- [ ] Content-Type header always set correctly for REST APIs
- [ ] API deprecation process documented (sunset headers, changelog)

---

## Next Steps

- [Kafka](./messaging/kafka.md) — Schema Registry integration with Avro/Protobuf
- [Service Mesh](./14-service-mesh.md) — gRPC load balancing and retries
- [Debugging](./22-debugging-distributed-systems.md) — debugging Protobuf and gRPC in production
