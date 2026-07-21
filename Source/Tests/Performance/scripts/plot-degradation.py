#!/usr/bin/env python3
"""
Generate performance degradation chart: p(95) latency & ThreadPool Queue vs VU count.
Uses summary JSON + counter CSV from all 4 test scenarios.

Usage:
  # Auto-discover from results directory:
  python plot-degradation.py --results-dir "D:\\path\\to\\perf tests results"

  # Use built-in defaults (no args):
  python plot-degradation.py

  # Manual per-scenario:
  python plot-degradation.py -s Smoke 2 summary.json counters.csv -s Load 50 ...
"""

import json
import csv
import re
import argparse
from pathlib import Path
import matplotlib.pyplot as plt
import matplotlib.ticker as ticker
import numpy as np

SCRIPT_DIR = Path(__file__).resolve().parent
RESULTS_DIR = SCRIPT_DIR.parent / "results"

# Known scenario types: folder prefix -> (label, default VUs)
SCENARIO_TYPES = [
    ("smoke", "Smoke", 2),
    ("load", "Load", 50),
    ("stress", "Stress", 200),
    ("ultra", "Ultra-Stress", 1000),
]

# Default scenarios (used when no --scenario and no --results-dir provided)
DEFAULT_SCENARIOS = [
    (
        "Smoke",
        2,
        RESULTS_DIR / "smoke 04.05.2026 20-41" / "smoke_summary_20260504_203926.json",
        RESULTS_DIR / "smoke 04.05.2026 20-41" / "counters_smoke_20260504_203926.csv",
    ),
    (
        "Load",
        50,
        RESULTS_DIR / "load 04.05.2026 20-47" / "load_summary_20260504_204222.json",
        RESULTS_DIR / "load 04.05.2026 20-47" / "counters_load_20260504_204222.csv",
    ),
    (
        "Stress",
        200,
        RESULTS_DIR / "stress 04.05.2026 20-58" / "stress_summary_20260504_204813.json",
        RESULTS_DIR / "stress 04.05.2026 20-58" / "counters_stress_20260504_204813.csv",
    ),
    (
        "Ultra-Stress",
        1000,
        RESULTS_DIR / "ultra stress 04.05.2026 21-47" / "ultra_stress_summary_20260504_213337.json",
        RESULTS_DIR / "ultra stress 04.05.2026 21-47" / "counters_ultra_stress_20260504_213337.csv",
    ),
]


def discover_scenarios(results_dir: Path) -> list:
    """Auto-discover scenarios from a results directory.

    Expects subdirectories like:
      smoke 04.05.2026 20-41/
      load 04.05.2026 20-47/
      stress 04.05.2026 20-58/
      ultra stress 04.05.2026 21-47/

    Each must contain *_summary_*.json and counters_*.csv.
    """
    results_dir = Path(results_dir)
    if not results_dir.is_dir():
        raise FileNotFoundError(f"Results directory not found: {results_dir}")

    scenarios = []
    for prefix, label, vus in SCENARIO_TYPES:
        # Find matching subdirectory
        matching_dirs = sorted(
            d for d in results_dir.iterdir()
            if d.is_dir() and d.name.lower().startswith(prefix)
        )
        if not matching_dirs:
            print(f"  Warning: no directory matching '{prefix}*' in {results_dir}")
            continue

        # Use the latest (last sorted) match
        subdir = matching_dirs[-1]

        # Find summary JSON
        summaries = list(subdir.glob("*_summary_*.json"))
        if not summaries:
            print(f"  Warning: no *_summary_*.json in {subdir.name}")
            continue

        # Find counters CSV
        counters = list(subdir.glob("counters_*.csv"))
        if not counters:
            print(f"  Warning: no counters_*.csv in {subdir.name}")
            continue

        scenarios.append((label, vus, summaries[-1], counters[-1]))
        print(f"  Found: {label} ({vus} VUs) -> {subdir.name}")

    return scenarios


def read_p95(summary_path: Path) -> dict:
    """Read latency percentiles from k6 summary JSON."""
    data = json.loads(summary_path.read_text(encoding="utf-8"))
    m = data["metrics"]["http_req_duration"]
    return {
        "p50": m.get("med", 0),
        "p90": m.get("p(90)", 0),
        "p95": m.get("p(95)", 0),
        "avg": m.get("avg", 0),
    }


def read_queue_stats(csv_path: Path) -> dict:
    """Read ThreadPool Queue Length stats from dotnet-counters CSV."""
    values = []
    with open(csv_path, encoding="utf-8") as f:
        reader = csv.DictReader(f)
        for row in reader:
            if row["Counter Name"] == "ThreadPool Queue Length":
                values.append(float(row["Mean/Increment"]))
    if not values:
        return {"avg": 0, "max": 0, "p95": 0}
    values.sort()
    idx_95 = int(len(values) * 0.95)
    return {
        "avg": sum(values) / len(values),
        "max": max(values),
        "p95": values[min(idx_95, len(values) - 1)],
    }


def read_cpu_stats(csv_path: Path) -> dict:
    """Read CPU Usage stats from dotnet-counters CSV."""
    values = []
    with open(csv_path, encoding="utf-8") as f:
        reader = csv.DictReader(f)
        for row in reader:
            if row["Counter Name"] == "CPU Usage (%)":
                values.append(float(row["Mean/Increment"]))
    if not values:
        return {"avg": 0, "max": 0}
    return {"avg": sum(values) / len(values), "max": max(values)}


def main():
    parser = argparse.ArgumentParser(
        description="Plot performance degradation chart",
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    parser.add_argument(
        "--output", "-o",
        default=None,
        help="Output image path (default: <results-dir>/degradation_chart.png)",
    )
    parser.add_argument(
        "--results-dir", "-d",
        default=None,
        help="Results directory to auto-discover scenarios from",
    )
    parser.add_argument(
        "--scenario", "-s",
        nargs=4, action="append", metavar=("LABEL", "VUS", "SUMMARY_JSON", "COUNTERS_CSV"),
        help="Add scenario manually: LABEL VUS SUMMARY_JSON COUNTERS_CSV (repeatable)",
    )
    args = parser.parse_args()

    # Determine scenarios
    if args.scenario:
        scenarios = [
            (label, int(vus), Path(summary), Path(counters))
            for label, vus, summary, counters in args.scenario
        ]
    elif args.results_dir:
        print(f"Scanning {args.results_dir}...")
        scenarios = discover_scenarios(Path(args.results_dir))
        if not scenarios:
            print("Error: no valid scenarios found.")
            return
    else:
        scenarios = DEFAULT_SCENARIOS

    # Default output path
    if args.output:
        output_path = args.output
    elif args.results_dir:
        output_path = str(Path(args.results_dir) / "degradation_chart.png")
    else:
        output_path = str(RESULTS_DIR / "degradation_chart.png")

    vus_list = []
    labels = []
    p95_list = []
    p50_list = []
    queue_avg_list = []
    queue_max_list = []
    cpu_avg_list = []

    for label, vus, summary_path, csv_path in scenarios:
        latency = read_p95(summary_path)
        queue = read_queue_stats(csv_path)
        cpu = read_cpu_stats(csv_path)

        vus_list.append(vus)
        labels.append(f"{label}\n({vus} VUs)")
        p95_list.append(latency["p95"])
        p50_list.append(latency["p50"])
        queue_avg_list.append(queue["avg"])
        queue_max_list.append(queue["max"])
        cpu_avg_list.append(cpu["avg"])

    # --- Plot ---
    fig, ax1 = plt.subplots(figsize=(10, 6))

    color_p95 = "#e74c3c"
    color_p50 = "#3498db"
    color_queue = "#2ecc71"

    x = np.arange(len(vus_list))

    # Left Y axis: latency (ms)
    ax1.set_xlabel("Сценарій навантаження", fontsize=12)
    ax1.set_ylabel("Час відповіді (мс)", fontsize=12, color=color_p95)

    line1 = ax1.plot(x, p95_list, "o-", color=color_p95, linewidth=2.5,
                     markersize=10, label="p(95) латентність", zorder=5)
    line2 = ax1.plot(x, p50_list, "s--", color=color_p50, linewidth=2,
                     markersize=8, label="p(50) латентність", zorder=5)

    # Annotate p95 values
    for i, (v, lbl) in enumerate(zip(p95_list, labels)):
        ax1.annotate(f"{v:.0f} мс", (x[i], v),
                     textcoords="offset points", xytext=(0, 14),
                     ha="center", fontsize=9, fontweight="bold", color=color_p95)

    ax1.tick_params(axis="y", labelcolor=color_p95)
    ax1.set_xticks(x)
    ax1.set_xticklabels(labels, fontsize=10)

    # Right Y axis: ThreadPool Queue
    ax2 = ax1.twinx()
    ax2.set_ylabel("ThreadPool Queue Length (max)", fontsize=12, color=color_queue)

    bar_width = 0.35
    bars = ax2.bar(x + 0.05, queue_max_list, bar_width, alpha=0.4,
                   color=color_queue, label="ThreadPool Queue (max)", zorder=2)

    # Annotate queue values
    for i, v in enumerate(queue_max_list):
        if v > 0:
            ax2.annotate(f"{v:.0f}", (x[i] + 0.05, v),
                         textcoords="offset points", xytext=(0, 5),
                         ha="center", fontsize=9, color=color_queue, fontweight="bold")

    ax2.tick_params(axis="y", labelcolor=color_queue)

    # Threshold line at 300ms (typical p95 target)
    ax1.axhline(y=300, color="#95a5a6", linestyle=":", linewidth=1.5, alpha=0.7)
    ax1.text(0.02, 310, "Поріг p(95) = 300 мс", fontsize=8, color="#7f8c8d",
             transform=ax1.get_yaxis_transform())

    # Combined legend
    lines = line1 + line2
    bar_proxy = plt.Rectangle((0, 0), 1, 1, fc=color_queue, alpha=0.4)
    all_handles = [l for l in lines] + [bar_proxy]
    all_labels = ["p(95) латентність", "p(50) латентність", "ThreadPool Queue (max)"]
    ax1.legend(all_handles, all_labels, loc="upper left", fontsize=10)

    plt.title("Деградація продуктивності Track List під навантаженням", fontsize=14, fontweight="bold")
    fig.tight_layout()

    plt.savefig(output_path, dpi=150, bbox_inches="tight")
    print(f"Chart saved to {output_path}")
    plt.close()


if __name__ == "__main__":
    main()
