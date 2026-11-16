# Planning capacity semantics

`Vehicle.HorseCapacity` is the maximum number of horses carried on a road leg. `Vehicle.StallCapacity` is the maximum number of individual horse stalls available on that vehicle. Since the current model assigns one stall per horse, a vehicle cannot be assigned to a leg with more horses than either limit. Planning enforces both limits when attaching a vehicle to an existing leg and when adding a horse to a leg with a vehicle.

`Stall.Capacity` is a separate facility resource count. `HorseLegAssignment.StallId` assigns a horse to one facility stall for a route leg; Planning checks overlapping assignments against that facility's capacity and readiness requires each assigned horse to have a stall. There is no direct `VehicleId` to `StallId` relationship in the current schema, so facility stalls are not treated as the vehicle's onboard stalls.
