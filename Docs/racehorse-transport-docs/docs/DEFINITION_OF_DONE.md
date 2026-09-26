# Definition of Done

## 1. Normal vertical-slice feature

A task is Done only when all applicable items are complete.

### Product

- business rule is implemented
- state transition is correct
- authorization is correct
- edge cases are considered

### Database

- entity/schema implemented in owning service
- migration included
- indexes/constraints reviewed
- no cross-service FK

### Backend

- endpoint implemented
- DTOs implemented
- validation implemented
- authorization implemented
- error codes added
- response follows API standard
- audit added for sensitive transitions
- event published/consumed if required

### Frontend

- API integration complete
- query/mutation hooks complete
- navigation wired
- screen implemented
- loading state
- empty state
- error state
- success feedback
- form validation
- permission/role UX

### End-to-end

- mobile → Gateway → service → DB path works
- event flow verified where applicable
- no hardcoded fake business success remains

### Testing

At minimum:

- backend unit tests for non-trivial domain/application rules
- endpoint/API tests for important commands
- FE component/hook tests where logic is non-trivial
- manual end-to-end verification of happy path and main failure path

## 2. Pull request checklist

```text
[ ] Correct service ownership
[ ] No cross-service DB access
[ ] API contract updated
[ ] Error code added
[ ] State transition matches specification
[ ] Frontend screen complete
[ ] Navigation wired
[ ] Loading/empty/error states handled
[ ] Authorization checked
[ ] Migration included
[ ] Event contract updated if needed
[ ] Tests added
[ ] Docker environment still starts
[ ] No secrets committed
```

## 3. Not Done examples

The following do NOT count as Done:

```text
API returns 200 but screen is not implemented

Screen uses mock array instead of real endpoint

Frontend changes status by PATCH without business command

Service reads another service's database

Member says "backend complete, FE next task"

Approval works but no authorization check

Document uploads but cannot be reviewed

Route change overwrites the active route instead of versioning
```
