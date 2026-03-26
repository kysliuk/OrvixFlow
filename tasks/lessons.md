## Workflow Orchestration
### 1.
Plan Node Default
﻿﻿Enter plan mode for ANY non-trivial task (3+ steps or architectural decisions)
﻿﻿If something goes sideways, STOP and re-plan immediately - don't keep pushing
﻿﻿Use plan mode for verification steps, not just building
﻿﻿Write detailed specs upfront to reduce ambiguity
### 2. Subagent Strategy
﻿﻿Use subagents liberally to keep main contect window clean
﻿﻿Offload research, exploration, and parallel analysis to subagents
﻿﻿For complex problens, throw more compute at it via subagents
﻿﻿One tack per subagent for focused execution
### 3. Self-Improvement Loop
﻿﻿After ANY correction from the user: update 'tasks/lessons.md" with the pattern
﻿﻿Write rules for yourself that prevent the same mistake
﻿﻿Ruthlessly iterate on these lessons until mistake rate drops
﻿﻿Review lessons at session start for relevant project
### 4. Verification Before Done
﻿﻿Never mark a task complete without proving it works
﻿﻿Diff behavior between main and your changes when relevant
﻿﻿Ask yourself: "Would a staff engineer approve this?"
﻿﻿Run tests, check logs, demonstrate correctness
### 5. Demand Elegance (Balanced)
﻿﻿For non-trivial changes: pause and ask "is there a more elegant way?"
﻿﻿If a fix feels hacky: "Knowing everything I know now, implement the elegant solution"
﻿﻿Skip this for simple, chvious fixes - don't over-engineer
﻿﻿Challenge your own work before presenting it
### 6. Autonomous Bug Fixing
﻿﻿When given a bug report: just fix it. Don't ask for hand-holding
﻿﻿Point at logs, errors, failing tests - then resolve them
﻿﻿Zero context switching required from the user
﻿﻿Go fix failing CI tests without being told how
## Task Management
## Core Principles
- Simplicity First: Make
every change as simple as possible. Inpact minimal code.
- **No
Laziness**: Find root
causes. No temporary fixes. Senior developer standards.
Minimal Impact; Changes should only touch what's necessary. Avoid introducing bugs.

### L2 — Workflow Orchestration
**Rule**: Always enter Plan Mode for non-trivial tasks (3+ steps). If something goes sideways, STOP and re-plan — do not push through. Use `subagents` to keep the main context clean, and always verify before marking "done". 
**Design Principle**: Demand elegance. Pause and ask, "Is there a more elegant way?" Do not settle for hacky fixes. Fix bugs autonomously when reported.

### L3 — Task Management
**Rule**: Maintain simplicity first with minimal impact. Track progress meticulously in `task.md`. Explain changes at each step, and document results in `walkthrough.md`.


---

## Lessons Learned

### L1 — Domain Constants: Use Enums, Not Hardcoded String Constants
**Mistake**: Created `Roles.cs` as `public static class Roles` with `public const string SuperAdmin = "SuperAdmin"` etc.
**Why it's wrong**:
- String constants scatter magic values — anything can pass a `string` role claim without validation.
- Enums give compile-time safety: invalid roles are impossible.
- Enums serialize cleanly and can be extended without risk of typo divergence.

**Rule**: Any domain concept repeated 3+ times (roles, statuses, scopes) → make it an `enum` in `OrvixFlow.Core`.
Store only the serialized string form in the DB/JWT; parse back to the enum immediately on the boundary.