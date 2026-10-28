# QIXR working instructions

## Runtime execution

The user explicitly requires on-demand execution. Do not start the QIXR gateway, launch/load inference or speech workers, enable automatic startup, or run live-service benchmarks unless the user explicitly requests runtime execution. Requests to design, write documentation, implement code, or prepare tests do not authorize starting services. Offline inspection and ordinary unit tests remain within the authorized development scope. Leave other projects' services unchanged.

The QIXR service was stopped and disabled at the user's request on 2026-10-28. Preserve that state until an explicit start request.

## Companion experience

English is the default product language. Korean is an explicit alternate locale. Agent prompts and implementation documentation should be in English. Answer user development questions in the language they use.

Preserve the interaction policy in `server/qixr_companion/prompts/quantum_agent.txt`: brief, grounded first answers; optional detail and experiments; no idle narration or automatic live operations. Scientific claims must come from validated snapshots/events or reviewed concept blocks. Keep private dialogue out of shared answers.
