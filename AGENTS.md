<!-- bang-skill:start -->
## Project rules (bang-skill)

1. **NEVER delete databases, code, or the project without the user's explicit confirmation in this session.** This covers `DROP`/`TRUNCATE`/`DELETE` without `WHERE`, deleting DB files or data volumes, destructive migration resets, `rm -rf`, deleting files/folders/branches, `git reset --hard`, `git clean -fd`, `git push --force`, and overwriting or emptying user files. First state exactly what would be deleted and the consequence, then ask and wait. One approval covers only that one action.
2. At session start, read `Project_proposal.md` and `task_board.md`.
3. Topic, scope, data, or pipeline changed -> update that section of `Project_proposal.md` and add a Changelog row.
4. Task assigned or status changed -> update its row in `task_board.md`.
5. After work -> add a Work log line to the current phase file in `Process_phases/`. Exit criteria met -> close the phase, open the next `NN_<phase>.md`, update `Process_phases/README.md`.
6. Edit these files in place; never rewrite or reset them.
7. If the ponytail skill is installed, work in `/ponytail:ponytail ultra` mode.
<!-- bang-skill:end -->
