import { Link } from "react-router-dom";
import {
  Bar,
  BarChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { useAuth } from "../hooks/useAuth";
import {
  useAdminSignups,
  useAdminStats,
  useAdminUsers,
  useDeleteUser,
  useGrantAdmin,
  useRevokeAdmin,
} from "../hooks/useAdmin";
import type { AdminStats, AdminUser, WeeklySignups } from "../types/admin";

const SIGNUP_COLOR = "#2980b9";

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

function joinedLabel(createdAt: string): string {
  return new Date(createdAt).toLocaleDateString(undefined, {
    year: "numeric",
    month: "short",
    day: "numeric",
  });
}

function StatTiles({ stats }: { stats: AdminStats }) {
  return (
    <div className="analytics-tiles">
      <div className="stat-tile">
        <span className="stat-label">Total users</span>
        <span className="stat-value">{stats.totalUsers}</span>
      </div>
      <div className="stat-tile">
        <span className="stat-label">New this week</span>
        <span className="stat-value">{stats.newUsersThisWeek}</span>
      </div>
      <div className="stat-tile">
        <span className="stat-label">Receipts</span>
        <span className="stat-value">{stats.totalReceipts}</span>
      </div>
      <div className="stat-tile">
        <span className="stat-label">Pantry items</span>
        <span className="stat-value">{stats.totalPantryItems}</span>
      </div>
    </div>
  );
}

function SignupsChart({ weekly }: { weekly: WeeklySignups[] }) {
  const data = weekly.map((week) => ({ ...week, label: weekLabel(week.weekStart) }));
  return (
    <section className="chart-section">
      <h2>Signups by week</h2>
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
          <Bar
            dataKey="count"
            name="Signups"
            fill={SIGNUP_COLOR}
            maxBarSize={24}
            radius={[4, 4, 0, 0]}
          />
        </BarChart>
      </ResponsiveContainer>
    </section>
  );
}

function UsersTable({ users }: { users: AdminUser[] }) {
  const { user: currentUser } = useAuth();
  const grantAdmin = useGrantAdmin();
  const revokeAdmin = useRevokeAdmin();
  const deleteUser = useDeleteUser();

  const mutationError =
    grantAdmin.error?.message ?? revokeAdmin.error?.message ?? deleteUser.error?.message;
  const pending = grantAdmin.isPending || revokeAdmin.isPending || deleteUser.isPending;

  function handleDelete(user: AdminUser) {
    if (window.confirm(`Delete ${user.email} and all their data?`)) {
      deleteUser.mutate(user.id);
    }
  }

  return (
    <section className="chart-section">
      <h2>Users</h2>
      {mutationError && <p className="error">{mutationError}</p>}
      <table className="most-tossed admin-users">
        <thead>
          <tr>
            <th>Email</th>
            <th>Joined</th>
            <th>Items</th>
            <th>Receipts</th>
            <th>Role</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {users.map((user) => {
            const isSelf = user.id === currentUser?.id;
            return (
              <tr key={user.id}>
                <td>{user.email}</td>
                <td>{joinedLabel(user.createdAt)}</td>
                <td>{user.pantryItemCount}</td>
                <td>{user.receiptCount}</td>
                <td>{user.isAdmin ? <span className="admin-badge">Admin</span> : "—"}</td>
                <td className="admin-actions">
                  {user.isAdmin ? (
                    <button
                      onClick={() => revokeAdmin.mutate(user.id)}
                      disabled={pending || isSelf}
                      title={
                        isSelf
                          ? "You can't remove your own admin role"
                          : "Remove admin role"
                      }
                    >
                      Remove admin
                    </button>
                  ) : (
                    <button
                      onClick={() => grantAdmin.mutate(user.id)}
                      disabled={pending}
                      title="Grant admin role"
                    >
                      Make admin
                    </button>
                  )}
                  <button
                    className="danger"
                    onClick={() => handleDelete(user)}
                    disabled={pending || isSelf}
                    title={
                      isSelf
                        ? "You can't delete your own account"
                        : "Delete user and all their data"
                    }
                  >
                    Delete
                  </button>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </section>
  );
}

export function AdminPage() {
  const stats = useAdminStats();
  const signups = useAdminSignups(12);
  const users = useAdminUsers();

  const isLoading = stats.isLoading || signups.isLoading || users.isLoading;
  const error = stats.error ?? signups.error ?? users.error;

  return (
    <main className="pantry analytics">
      <header className="pantry-header">
        <h1>🛠 Admin</h1>
      </header>

      <nav className="pantry-nav">
        <Link to="/pantry" className="button-link">
          ← Pantry
        </Link>
      </nav>

      {isLoading && <p className="loading">Loading admin data…</p>}
      {error && <p className="error">Couldn't load admin data: {error.message}</p>}
      {!isLoading && !error && (
        <>
          {stats.data && <StatTiles stats={stats.data} />}
          {signups.data && <SignupsChart weekly={signups.data} />}
          {users.data && <UsersTable users={users.data} />}
        </>
      )}
    </main>
  );
}
