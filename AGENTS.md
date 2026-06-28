## Priorities
- Follow Preagonal.Scripting.GS2Engine style exactly to keep a clean and consistent code base.

- Correctness percentage is the top priority.
- Correctness percentage per function is important.
- Size comes second, but linker blockers still need to move forward.
- Do not make changes that lower correctness just to clear linker blockers.
- If correctness drops, revert or rework the change.

- You need to make commits/pushes at proper milestones so you don't lose your place in case of loss of correctness and you need to revert changes we have it and aren't left with broken code.

## Prohibited

- No shims or fake patches.
- Avoid gotos as source cleanup or reshaping.
- DO NOT MAKE UP SHIT THAT'S NOT IN THE ORIGINAL, UNLESS IT'S AN INLINE GLOBAL HELPER THAT MAKES SENSE IN THE BROADER SCALE AND CAN BE REUSED FOR MULTIPLE FUNCTIONS TO HELP GET THE CORRECTNESS PERCENTAGE UP. THE HELPER MUST BE INLINED SO THERE ARE NO SUPERFLUOUS CALLS TO NEW BULLSHIT FUNCTIONS.
