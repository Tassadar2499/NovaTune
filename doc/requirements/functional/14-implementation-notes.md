# 4. Implementation Notes (current repo state)

- The API project currently exposes only sample endpoints (`/` and `/weatherforecast`) and sets up KafkaFlow + Garnet cache; the controllers/services shown in `doc/diagrams/component.puml` are not yet implemented.
- The event types and KafkaFlow consumers/producers are present, along with handler stubs and cache invalidation behavior for deletions.
