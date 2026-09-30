import { useEffect, useRef } from "react";
import { Chart, registerables, type ChartType } from "chart.js";

Chart.register(...registerables);

export interface ReportChartDataset {
  label: string;
  data: number[];
  color: string;
}

interface ReportChartProps {
  type: Extract<ChartType, "line" | "bar">;
  labels: string[];
  datasets: ReportChartDataset[];
  ariaLabel: string;
  height?: number;
}

export function ReportChart({ type, labels, datasets, ariaLabel, height = 240 }: ReportChartProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const chartRef = useRef<Chart | null>(null);

  useEffect(() => {
    if (!canvasRef.current) {
      return;
    }

    chartRef.current?.destroy();
    chartRef.current = new Chart(canvasRef.current, {
      type,
      data: {
        labels,
        datasets: datasets.map((dataset) => ({
          label: dataset.label,
          data: dataset.data,
          borderColor: dataset.color,
          backgroundColor: type === "bar" ? dataset.color : `${dataset.color}33`,
          borderWidth: 2,
          borderRadius: type === "bar" ? 4 : 0,
          fill: type === "line" && datasets.length === 1,
          tension: 0.2,
          pointRadius: type === "line" ? 3 : 0,
        })),
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { display: datasets.length > 1 },
        },
        scales: {
          x: { grid: { display: false } },
          y: { beginAtZero: true, grid: { color: "rgba(128,128,128,0.15)" } },
        },
      },
    });

    return () => chartRef.current?.destroy();
  }, [type, labels, datasets]);

  return (
    <div style={{ position: "relative", height }}>
      <canvas ref={canvasRef} role="img" aria-label={ariaLabel} />
    </div>
  );
}
