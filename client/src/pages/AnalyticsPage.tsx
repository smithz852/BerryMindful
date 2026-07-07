import { useState } from "react";
import { Link } from "react-router-dom";
import {
  Bar,
  BarChart,
  CartesianGrid,
  LabelList,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { useWasteAnalytics } from "../hooks/useWasteAnalytics";
import type {
  CategoryWaste,
  TossedItem,
  WasteTotals,
  WeeklyWaste,
} from "../types/analytics";

const USED_COLOR = "#27ae60";
const TOSSED_COLOR = "#c0392b";

const RANGES = [
  { days: 30, label: "30 days" },
  { days: 90, label: "90 days" },
  { days: 0, label: "All time" },
] as const;

const tooltipContentStyle = {
  background: "var(--bg)",
  border: "1px solid rgba(127, 127, 127, 0.35)",
  borderRadius: 6,
};
const tooltipTextStyle = { color: "var(--text)" };

function weekLabel(weekStart: string): string {
  // Server sends a UTC date; parse the date part as local so it never shifts a day.
  return new Date(weekStart.slice(0, 10) + "T00:00:00").toLocaleDateString(
    undefined,
    { month: "short", day: "numeric" },
  );
}

function RangeSelector({
  days,
  onChange,
}: {
  days: number;
  onChange: (days: number) => void;
}) {
  return (
    <div className="range-selector" role="group" aria-label="Time range">
      {RANGES.map((range) => (
        <button
          key={range.days}
          className={range.days === days ? "active" : ""}
          onClick={() => onChange(range.days)}
        >
          {range.label}
        </button>
      ))}
    </div>
  );
}

function StatTiles({ totals }: { totals: WasteTotals }) {
  return (
    <div className="analytics-tiles">
      <div className="stat-tile">
        <span className="stat-label">Items used</span>
        <span className="stat-value">{totals.used}</span>
      </div>
      <div className="stat-tile">
        <span className="stat-label">Items tossed</span>
        <span className="stat-value">{totals.tossed}</span>
        <span className="stat-sub">{totals.tossedAfterExpiry} after expiry</span>
      </div>
      <div className="stat-tile">
        <span className="stat-label">Waste rate</span>
        <span className="stat-value">{Math.round(totals.wasteRate * 100)}%</span>
      </div>
    </div>
  );
}

function ChartLegend() {
  return (
    <div className="chart-legend">
      <span>
        <i style={{ background: USED_COLOR }} /> Used
      </span>
      <span>
        <i style={{ background: TOSSED_COLOR }} /> Tossed
      </span>
    </div>
  );
}

function WeeklyChart({ weekly }: { weekly: WeeklyWaste[] }) {
  const data = weekly.map((week) => ({ ...week, label: weekLabel(week.weekStart) }));
  return (
    <section className="chart-section">
      <h2>Used vs tossed by week</h2>
      <ChartLegend />
      <ResponsiveContainer width="100%" height={220}>
        <BarChart data={data} margin={{ top: 4, right: 4, bottom: 0, left: 0 }}>
          <CartesianGrid vertical={false} strokeOpacity={0.15} />
          <XAxis
            dataKey="label"
            tickLine={false}
            axisLine={false}
            minTickGap={24}
          />
          <YAxis allowDecimals={false} tickLine={false} axisLine={false} width={28} />
          <Tooltip
            cursor={{ fillOpacity: 0.06 }}
            contentStyle={tooltipContentStyle}
            itemStyle={tooltipTextStyle}
            labelStyle={tooltipTextStyle}
          />
          <Bar dataKey="used" name="Used" stackId="week" fill={USED_COLOR} maxBarSize={24} />
          <Bar
            dataKey="tossed"
            name="Tossed"
            stackId="week"
            fill={TOSSED_COLOR}
            maxBarSize={24}
            radius={[4, 4, 0, 0]}
          />
        </BarChart>
      </ResponsiveContainer>
    </section>
  );
}

function CategoryChart({ byCategory }: { byCategory: CategoryWaste[] }) {
  const data = byCategory.filter((entry) => entry.tossed > 0);
  if (data.length === 0) {
    return null;
  }
  return (
    <section className="chart-section">
      <h2>Tossed by category</h2>
      <ResponsiveContainer width="100%" height={data.length * 36 + 8}>
        <BarChart
          data={data}
          layout="vertical"
          margin={{ top: 0, right: 28, bottom: 0, left: 0 }}
        >
          <XAxis type="number" hide />
          <YAxis
            type="category"
            dataKey="category"
            tickLine={false}
            axisLine={false}
            width={80}
          />
          <Tooltip
            cursor={{ fillOpacity: 0.06 }}
            contentStyle={tooltipContentStyle}
            itemStyle={tooltipTextStyle}
            labelStyle={tooltipTextStyle}
          />
          <Bar
            dataKey="tossed"
            name="Tossed"
            fill={TOSSED_COLOR}
            maxBarSize={20}
            radius={[0, 4, 4, 0]}
          >
            <LabelList dataKey="tossed" position="right" />
          </Bar>
        </BarChart>
      </ResponsiveContainer>
    </section>
  );
}

function MostTossedList({ items }: { items: TossedItem[] }) {
  if (items.length === 0) {
    return null;
  }
  return (
    <section className="chart-section">
      <h2>Most tossed items</h2>
      <table className="most-tossed">
        <thead>
          <tr>
            <th>Item</th>
            <th>Times tossed</th>
          </tr>
        </thead>
        <tbody>
          {items.map((item) => (
            <tr key={item.name}>
              <td>{item.name}</td>
              <td>{item.count}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}

export function AnalyticsPage() {
  const [days, setDays] = useState(90);
  const { data, isLoading, error } = useWasteAnalytics(days);
  const hasData = data !== undefined && data.totals.used + data.totals.tossed > 0;

  return (
    <main className="pantry analytics">
      <header className="pantry-header">
        <h1>📊 Waste Analytics</h1>
      </header>

      <nav className="pantry-nav">
        <Link to="/pantry" className="button-link">
          ← Pantry
        </Link>
      </nav>

      <RangeSelector days={days} onChange={setDays} />

      {isLoading && <p className="loading">Loading analytics…</p>}
      {error && <p className="error">Couldn't load analytics: {error.message}</p>}
      {data && !hasData && (
        <p className="empty">
          No used or tossed items in this range yet — mark items ✓ Used or 🗑
          Tossed from the pantry and check back.
        </p>
      )}
      {hasData && (
        <>
          <StatTiles totals={data.totals} />
          <WeeklyChart weekly={data.weekly} />
          <CategoryChart byCategory={data.byCategory} />
          <MostTossedList items={data.mostTossed} />
        </>
      )}
    </main>
  );
}
