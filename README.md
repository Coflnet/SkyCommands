# SkyCommands
Command and API service

Handles external Http requests and potentially forwards them to other services.

Common env variables:
- JAEGER_SERVICE_NAME="skyblock-commands"
- JAEGER_AGENT_HOST=jaeger
- KAFKA_HOST=kafka:9092

Additional configuration can be found in `appsettings.json`.
You can overwrite it via Enviroment variables. 

> **Note**: the keys represent the JSON path of a value and `:` has to be replaced with `__`. eg `TOPICS:FLIP_CONSUME` becomes `TOPICS__FLIP_CONSUME`
