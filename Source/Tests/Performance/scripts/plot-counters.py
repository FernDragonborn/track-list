#!/usr/bin/env python3
"""
Generate server metrics chart from dotnet-counters CSV.
Shows CPU, Working Set (RAM), GC Heap, and ThreadPool Queue over time.

Usage:
  python plot-counters.py <counters.csv> [-o output.png] [--title TITLE]
"""

import csv
import argparse
from pathlib import Path
from datetime import datetime
import matplotlib.pyplot as plt
import matplotlib.dates as mdates
import numpy as np


def read_counters(csv_path: Path) -> dict:
    """Read all counter time series from CSV."""
    series = {}
    with open(csv_path, encoding="utf-8") as f:
        reader = csv.DictReader(f)
        for row in reader:
            name = row["Counter Name"]
            ts = datetime.strptime(row["Timestamp"], "%m/%d/%Y %H:%M:%S")
            val = float(row["Mean/Increment"])
            if name not in series:
                series[name] = {"ts": [], "vals": []}
            series[name]["ts"].append(ts)
            series[name]["vals"].append(val)
    return series


def main():
    parser = argparse.ArgumentParser(description="Plot dotnet-counters metrics")
    parser.add_argument("csv_file", help="Path to counters CSV file")
    parser.add_argument("--output", "-o", default=None,
                        help="Output image path (default: same dir as CSV)")
    parser.add_argument("--title", "-t", default=None,
                        help="Chart title (default: auto from filename)")
    args = parser.parse_args()

    csv_path = Path(args.csv_file)
    if not csv_path.exists():
        print(f"Error: {csv_path} not found")
        return

    output = args.output or str(csv_path.with_suffix(".png"))
    title = args.title or f"Серверні метрики: {csv_path.stem}"

    series = read_counters(csv_path)

    # 4-panel chart: CPU, RAM, GC Heap, ThreadPool
    fig, axes = plt.subplots(4, 1, figsize=(12, 10), sharex=True)
    fig.suptitle(title, fontsize=14, fontweight="bold")

    panels = [
        ("CPU Usage (%)", axes[0], "#e74c3c", "CPU, %"),
        ("Working Set (MB)", axes[1], "#3498db", "Working Set, МБ"),
        ("GC Heap Size (MB)", axes[2], "#2ecc71", "GC Heap, МБ"),
        ("ThreadPool Queue Length", axes[3], "#9b59b6", "ThreadPool Queue"),
    ]

    for counter_name, ax, color, ylabel in panels:
        if counter_name not in series:
            ax.text(0.5, 0.5, f"Немає даних: {counter_name}",
                    transform=ax.transAxes, ha="center", va="center")
            ax.set_ylabel(ylabel, fontsize=10)
            continue

        data = series[counter_name]
        ts = data["ts"]
        vals = data["vals"]

        ax.plot(ts, vals, color=color, linewidth=1.2, alpha=0.8)
        ax.fill_between(ts, vals, alpha=0.15, color=color)
        ax.set_ylabel(ylabel, fontsize=10)
        ax.grid(True, alpha=0.3)

        # Stats annotation
        avg_val = sum(vals) / len(vals)
        max_val = max(vals)
        ax.axhline(y=avg_val, color=color, linestyle="--", linewidth=0.8, alpha=0.5)
        ax.text(0.98, 0.92, f"avg={avg_val:.1f}  max={max_val:.1f}",
                transform=ax.transAxes, ha="right", va="top",
                fontsize=9, color=color, fontweight="bold",
                bbox=dict(boxstyle="round,pad=0.3", facecolor="white", alpha=0.8))

    # X-axis formatting
    axes[-1].xaxis.set_major_formatter(mdates.DateFormatter("%H:%M"))
    axes[-1].set_xlabel("Час", fontsize=11)
    plt.xticks(rotation=45)

    # Also add ThreadPool Thread Count as secondary line if available
    if "ThreadPool Thread Count" in series:
        ax_threads = axes[3].twinx()
        data = series["ThreadPool Thread Count"]
        ax_threads.plot(data["ts"], data["vals"], color="#e67e22",
                       linewidth=1, alpha=0.6, linestyle="--")
        ax_threads.set_ylabel("Threads", fontsize=9, color="#e67e22")
        ax_threads.tick_params(axis="y", labelcolor="#e67e22")

    fig.tight_layout()
    plt.savefig(output, dpi=150, bbox_inches="tight")
    print(f"Chart saved to {output}")
    plt.close()


if __name__ == "__main__":
    main()
