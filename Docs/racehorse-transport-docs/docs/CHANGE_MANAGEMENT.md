# Contract Change Management

## 1. Changes requiring review

The following are shared contracts:

- API response envelope
- request/response DTOs
- state machine
- event schema
- database ownership
- URL/versioning
- role permissions

A member must not silently change any of these.

## 2. Non-breaking change

Examples:

- add an optional response field
- add a new endpoint
- add a new enum state only when all clients can safely ignore it and domain semantics are reviewed

## 3. Breaking change

Examples:

- rename an existing JSON field
- remove a response field
- change a status meaning
- change required field to a different type
- delete an event field consumers need

Breaking changes require:

1. contract update
2. affected FE/service review
3. versioning strategy
4. migration plan

## 4. PR rule

PR description must state:

```text
Contract changed: Yes/No
Database changed: Yes/No
Event changed: Yes/No
Frontend screen changed: Yes/No
Breaking change: Yes/No
```
