## Newest update

Created at 8:22PM 20/07/2026.
Based on docs/plans/2026-07-20-epic1-reopen-owner-reclosure-runbook.md, these are my findings and decions

- Step 0 & 1 passed, all expectations met
- Step 2: **Notice** every dropdown filter show a dim cell, **NO** block appear (this is my intention as i required to hide all tasks and subjects that are **not created by the user**, which means hidden items are in the training seed for ML), however, the heat map **does not** re-render on every subject. Another key finding is that the graph demonstrating " số phút học 7 ngày qua" of subject "A" was abnormal as the only task inside the subject was previously marked "Đã xong" while the analytics graph show "1", and this phenomenon **persists** every time i run, especially on different days. Additionally, this graph does not re-render correctly in real time when i change the option in drop down, different orders in options creates different graph rendering. Some subject would show a small section stating "Không có dữ liệu cho bộ lọc hiện tại" but the graph still renders (probably copied from previous options).
- Step 3: passed
- Step 4: sign off
- Step 5: acknowledged, however advise me about this, whether to open an additional tech-debt fix before next epic.
- Step 6: do not release yet, dispatch a team of agents to investigate my finding in step 2 before coming into a conclusion. 

I will send some images about my finding in step 2 in my answer prompt.