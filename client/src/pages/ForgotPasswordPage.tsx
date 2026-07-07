import { useState, type FormEvent } from "react";
import { api } from "../api/client";

export function ForgotPasswordPage() {
  const [email, setEmail] = useState("");
  const [sent, setSent] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      await api("/auth/forgot-password", {
        method: "POST",
        body: JSON.stringify({ email }),
      });
      setSent(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Something went wrong.");
    } finally {
      setSubmitting(false);
    }
  }

  if (sent) {
    return (
      <main className="auth-page">
        <h1>🫐 BerryMindful</h1>
        <h2>Check your email</h2>
        <p>
          If an account exists for {email}, a password reset link is on its
          way. The link expires in 1 hour.
        </p>
        <p>
          <a href="/login">Back to log in</a>
        </p>
      </main>
    );
  }

  return (
    <main className="auth-page">
      <h1>🫐 BerryMindful</h1>
      <h2>Forgot password</h2>
      <p>Enter your email and we&apos;ll send you a reset link.</p>
      <form onSubmit={handleSubmit}>
        <label>
          Email
          <input
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
            autoComplete="email"
          />
        </label>
        {error && <p className="error">{error}</p>}
        <button type="submit" disabled={submitting}>
          {submitting ? "..." : "Send reset link"}
        </button>
      </form>
      <p>
        <a href="/login">Back to log in</a>
      </p>
    </main>
  );
}
