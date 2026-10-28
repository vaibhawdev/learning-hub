# Graph Databases & Neo4j

> **License quick-ref** — Neo4j Community: open-source (GPL-3); Neo4j Enterprise: commercial; Neo4j AuraDB: managed SaaS. Amazon Neptune: managed SaaS. ArangoDB Community: Apache-2; ArangoDB Enterprise: commercial. JanusGraph: Apache-2 (fully open-source).

---

## Why Graph Databases?

Relational databases store data in tables and express relationships via foreign keys and JOINs. This works well until:

- Relationships **are** the primary data (social networks, org charts, knowledge graphs)
- Queries require **traversing many levels of connections** (friends-of-friends, shortest path, impact analysis)
- The relationship schema is **highly variable** (different entity types connect in different ways)

A relational JOIN across 6 hops is exponentially expensive. A graph traversal of the same depth is constant-time per hop regardless of graph size.

```
Relational (6-hop friends query):
SELECT * FROM users u1
JOIN friends f1 ON u1.id = f1.user_id
JOIN users u2 ON f1.friend_id = u2.id
JOIN friends f2 ON u2.id = f2.user_id
... (6 joins, index lookups, cartesian explosion)

Graph (Cypher, same query):
MATCH (you)-[:FRIEND*1..6]-(person)
WHERE you.name = 'Alice'
RETURN person
```

---

## Core Graph Concepts

### Property Graph Model

The most common model (used by Neo4j, Amazon Neptune, TigerGraph):

- **Node**: An entity (Person, Product, Account). Has a label and properties.
- **Relationship**: A directed, named connection between two nodes. Has a type and properties.
- **Property**: Key-value pair on nodes or relationships.
- **Label**: Type tag on a node (can have multiple).

```
(:Person {name: "Alice", age: 30})-[:KNOWS {since: 2020}]->(:Person {name: "Bob"})
(:Person {name: "Alice"})-[:PURCHASED {date: "2026-01-01"}]->(:Product {name: "Laptop"})
```

### RDF / Triple Store

Used by semantic web and knowledge graph applications. Data as subject-predicate-object triples:

```
Alice  KNOWS  Bob
Alice  AGE    30
```

RDF stores: Apache Jena, Stardog, GraphDB. Use when W3C standards (SPARQL, OWL) or semantic reasoning are required.

---

## Neo4j

Neo4j is the most widely used graph database. Stores data natively as a graph (not a relational DB with a graph overlay), enabling constant-time relationship traversal.

### Architecture

```
Neo4j Cluster (Enterprise / AuraDB):
┌──────────┐    ┌──────────┐    ┌──────────┐
│  Primary  │◄──►│  Primary  │◄──►│  Primary  │
│ (leader)  │    │          │    │          │
└──────────┘    └──────────┘    └──────────┘
       ↓ Raft consensus
Read replicas (scales read throughput)
```

Community edition: single instance only. Enterprise: clustering, RBAC, hot backup.

### Cypher Query Language

Cypher is Neo4j's declarative query language. Patterns are written visually using `()` for nodes and `-->` for relationships.

```cypher
// Create
CREATE (:Person {name: 'Alice', age: 30})
CREATE (:Person {name: 'Bob', age: 25})
MATCH (a:Person {name: 'Alice'}), (b:Person {name: 'Bob'})
CREATE (a)-[:KNOWS {since: 2020}]->(b)

// Find all friends of Alice
MATCH (alice:Person {name: 'Alice'})-[:KNOWS]->(friend)
RETURN friend.name

// Friends of friends (2 hops)
MATCH (alice:Person {name: 'Alice'})-[:KNOWS*2]-(fof)
RETURN DISTINCT fof.name

// Shortest path between two people
MATCH path = shortestPath(
  (alice:Person {name: 'Alice'})-[:KNOWS*]-(target:Person {name: 'Charlie'})
)
RETURN path, length(path)

// Pattern matching: people who bought the same product
MATCH (p1:Person)-[:PURCHASED]->(product:Product)<-[:PURCHASED]-(p2:Person)
WHERE p1 <> p2
RETURN p1.name, p2.name, product.name

// Aggregation
MATCH (p:Person)-[:PURCHASED]->(product)
RETURN product.name, COUNT(p) AS buyers ORDER BY buyers DESC LIMIT 10

// Update
MATCH (p:Person {name: 'Alice'})
SET p.age = 31, p.city = 'London'

// Delete relationship
MATCH (a)-[r:KNOWS]->(b)
WHERE a.name = 'Alice' AND b.name = 'Bob'
DELETE r
```

### APOC (Awesome Procedures On Cypher)

Extension library with 400+ procedures for data import, graph algorithms, refactoring:

```cypher
// Import JSON
CALL apoc.load.json("https://api.example.com/data") YIELD value
MERGE (p:Person {id: value.id}) SET p.name = value.name

// Export to CSV
CALL apoc.export.csv.all("export.csv", {})

// Parallel relationship creation
CALL apoc.periodic.iterate(
  "MATCH (p:Person) RETURN p",
  "MATCH (p)-[:SIMILAR_TO]->(other) WHERE score(p, other) > 0.8 MERGE (p)-[:SIMILAR_TO]->(other)",
  {batchSize: 1000, parallel: true}
)
```

### GDS (Graph Data Science Library)

Graph algorithms built into Neo4j for ML pipelines and analytics:

```cypher
// PageRank
CALL gds.pageRank.stream('myGraph')
YIELD nodeId, score
RETURN gds.util.asNode(nodeId).name, score ORDER BY score DESC

// Community detection (Louvain)
CALL gds.louvain.stream('myGraph')
YIELD nodeId, communityId

// Node similarity
CALL gds.nodeSimilarity.stream('myGraph')
YIELD node1, node2, similarity
RETURN gds.util.asNode(node1).name, gds.util.asNode(node2).name, similarity

// Shortest path (Dijkstra)
CALL gds.shortestPath.dijkstra.stream('myGraph', {
  sourceNode: sourceId,
  relationshipWeightProperty: 'cost'
})
YIELD path, totalCost
```

---

## When to Use a Graph Database

### Strong Fit

| Use Case | Why Graph Wins |
|----------|---------------|
| Social networks (friend recommendations) | Deep traversal, variable-depth paths |
| Fraud detection (connected account rings) | Detect circular patterns across millions of nodes |
| Knowledge graphs / recommendation engines | Heterogeneous entities, multi-hop inference |
| Network / IT infrastructure topology | Model connected devices, find impact paths |
| Identity and access management | "Who can access what through which role chain?" |
| Supply chain traceability | Multi-hop upstream/downstream tracking |
| Org chart / hierarchy | Ancestor/descendant queries |
| Dependency analysis (code, packages) | Transitive dependency resolution |

### Poor Fit

| Use Case | Better Alternative |
|----------|-------------------|
| Tabular reports, aggregations | PostgreSQL, ClickHouse |
| Heavy write/time-series data | TimescaleDB, InfluxDB |
| Full-text search | Elasticsearch |
| Simple key-value lookup | Redis, DynamoDB |
| Document storage (nested objects) | MongoDB |

---

## Graph Databases Comparison

| Database | Model | License | Query Language | Best For |
|----------|-------|---------|---------------|---------|
| **Neo4j** | Property Graph | Community: GPL-3 / Enterprise: Commercial | Cypher | General-purpose, mature ecosystem |
| **Amazon Neptune** | Property Graph + RDF | Managed SaaS | Gremlin / SPARQL / openCypher | AWS-native, serverless |
| **ArangoDB** | Multi-model (graph + document + key-value) | Community: Apache-2 / Enterprise: Commercial | AQL | Multi-model, avoid polyglot DB overhead |
| **JanusGraph** | Property Graph | Apache-2 (fully OSS) | Gremlin | Large-scale, HBase/Cassandra backend |
| **TigerGraph** | Property Graph | Community: free / Enterprise: Commercial | GSQL | Large-scale analytics, ML pipelines |
| **RedisGraph** | Property Graph | Business Source (not fully OSS) | Cypher | Low latency, Redis integration |
| **Dgraph** | Property Graph | Apache-2 (core) / Commercial (features) | DQL (GraphQL variant) | Distributed-first design |
| **MemGraph** | Property Graph | BSL + Community | Cypher | In-memory, real-time analytics |

### Key Differences

**Neo4j vs ArangoDB**: ArangoDB is a multi-model database — one system handles graphs, documents, and key-value. Choose ArangoDB if you want to avoid running separate databases and your use case mixes models. Choose Neo4j if graph is the primary access pattern and you want the richest graph tooling.

**Neo4j vs Amazon Neptune**: Neptune is managed (no ops) and supports both Gremlin and openCypher. Choose Neptune on AWS when you want zero operational overhead. Choose Neo4j for deeper graph algorithm support (GDS) or self-hosted control.

**Neo4j vs JanusGraph**: JanusGraph scales horizontally using Cassandra or HBase as the storage backend — suitable for trillion-edge graphs. Neo4j scales vertically and via read replicas. Choose JanusGraph for internet-scale graphs with massive write throughput.

---

## Data Modeling for Graphs

### Think Relationships First

```
Bad model (relational thinking):
(:User)-[:HAS_ORDER]->(:Order)-[:HAS_ITEM]->(:OrderItem)-[:IS_PRODUCT]->(:Product)

Better (graph thinking):
(:User)-[:ORDERED {date: ..., qty: 2}]->(:Product {name: "Laptop"})
Enrich the relationship, minimize intermediate nodes
```

### Intermediate Nodes for Rich Relationships

When a relationship itself has many properties or links to other entities:

```
(:Employee)-[:WORKED_AT]->(:Company)  -- simple

(:Employee)-[:EMPLOYED_BY]->(:Employment {from: 2020, to: 2023, role: "Engineer"})->[:AT]->(:Company)
-- if Employment has its own relationships (benefits, manager, etc.)
```

### Avoid Supernodes

A supernode is a node with millions of relationships (e.g., a very popular celebrity in a social graph). Traversals through supernodes are slow.

**Mitigations:**
- Filter traversals early (use WHERE before expanding)
- Limit traversal depth
- Materialize common subgraphs as separate nodes
- Use relationship properties to filter before expanding

---

## Integration with Distributed Systems

### Neo4j + Kafka (Change Data Capture)

Stream graph changes to Kafka for downstream systems:

```yaml
# Neo4j Streams plugin (community) or Kafka Connect (enterprise)
# neo4j.conf
streams.sink.enabled=true
streams.sink.topic.pattern=neo4j-events:{*}
```

### Neo4j + Microservices

The graph database should be owned by a single service (Graph Service). Other services query via REST/GraphQL API, never directly connecting to Neo4j:

```
Graph Service (owns Neo4j)
  ├── POST /api/graph/relationships
  ├── GET  /api/graph/fraud-ring/{account_id}
  └── GET  /api/graph/recommendations/{user_id}
```

---

## Performance and Production

### Indexes

```cypher
// Node label + property index (most common)
CREATE INDEX person_name FOR (p:Person) ON (p.name)

// Composite index
CREATE INDEX FOR (p:Person) ON (p.lastName, p.firstName)

// Full-text index
CREATE FULLTEXT INDEX product_search FOR (n:Product) ON EACH [n.name, n.description]

// Check indexes
SHOW INDEXES
```

### Memory Configuration

```
# neo4j.conf
server.memory.heap.initial_size=4g    # JVM heap
server.memory.heap.max_size=4g
server.memory.pagecache.size=8g       # graph data in native memory
```

**Rule of thumb**: Page cache should hold the working set of frequently accessed nodes and relationships. Heap handles query processing.

### Query Profiling

```cypher
PROFILE MATCH (p:Person)-[:KNOWS*1..3]-(other)
WHERE p.name = 'Alice'
RETURN other.name

-- Shows: DbHits (disk reads), Rows (intermediate), operators used
```

`EXPLAIN` shows the plan without running; `PROFILE` runs and shows actual costs.

---

## Production Checklist

- [ ] Indexes created on all properties used in MATCH WHERE clauses
- [ ] Node and relationship counts monitored (growth rate planning)
- [ ] Memory: heap + pagecache sized to working dataset
- [ ] Clustering enabled (Enterprise) or backup to cloud storage (Community)
- [ ] Query timeout set (`dbms.transaction.timeout`) — prevent runaway traversals
- [ ] Causal cluster with read replicas for read-heavy workloads
- [ ] APOC and GDS installed if needed (check compatibility matrix)
- [ ] No queries without indexes on label properties (use `SHOW INDEXES`)
- [ ] Cypher query logging enabled for slow query analysis
- [ ] Supernode mitigation plan for high-degree nodes

---

## Next Steps

- [Database Sharding](./03-database-sharding.md) — when relational databases reach scale
- [Design Patterns](./19-design-patterns.md) — CQRS with graph as read model
- [Observability](./16-observability.md) — monitoring Neo4j with Prometheus exporter
