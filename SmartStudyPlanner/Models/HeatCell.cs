using System;

namespace SmartStudyPlanner.Models
{
    public record HeatCell(DateTime Date, int TotalMinutes, int Level)
    {
        public string Tooltip => TotalMinutes == 0
            ? $"{Date:dd/MM/yyyy} — Không có dữ liệu"
            : $"{Date:dd/MM/yyyy} — {TotalMinutes} phút";
    }
}
