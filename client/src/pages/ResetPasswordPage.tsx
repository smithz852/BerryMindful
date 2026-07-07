import { useState, type FormEvent } from "react";
import { useSearchParams } from "react-router-dom";
import { api } from "../api/client";

export function ResetPasswordPage() {
  const [searchParams] = useSearchParams();
  const token = searchParams.get("token");
  const email = searchParams.get("email");

  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [done, setDone] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  if (!token || !email) {
    return (
      <main className="auth-page">
        <h1>🫐 BerryMindful</h1>
        <h2>Invalid reset link</h2>
        <p>
          This link is missing its reset token. Request a new one from the
          forgot-password page.
        </p>
        <p>
          <a href="/forgot-password">Request a new link</a>
        </p>
      </main>
    );
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (password !== confirm) {
      setError("Passwords don't match.");
      return;
    }
    setError(null);
    setSubmitting(true);
    try {
      await api("/auth/reset-password", {
        method: "POST",
        body: JSON.stringify({ email, token, newPassword: password }),
      });
      setDone(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Something went wrong.");
    } finally {
      setSubmitting(false);
    }
  }

  if (done) {
    return (
      <main className="auth-page">
        <h1>🫐 BerryMindful</h1>
        <h2>Password updated</h2>
        <p>
          Your password has been reset and all other sessions were signed out.
        </p>
        <p>
          <a href="/login">Log in with your new password</a>
        </p>
      </main>
    );
  }

  return (
    <main className="auth-page">
      <h1>🫐 BerryMindful</h1>
      <h2>Choose a new password</h2>
      <form onSubmit={handleSubmit}>
        <label>
          New password
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
            minLength={8}
            autoComplete="new-password"
          />
        </label>
        <label>
          Confirm new password
          <input
            type="password"
            value={confirm}
            onChange={(e) => setConfirm(e.target.value)}
            required
            minLength={8}
            autoComplete="new-password"
          />
        </label>
        {error && <p className="error">{error}</p>}
        <button type="submit" disabled={submitting}>
          {submitting ? "..." : "Reset password"}
        </button>
      </form>
    </main>
  );
}
