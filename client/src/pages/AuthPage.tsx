import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../hooks/useAuth";

export function AuthPage({ mode }: { mode: "login" | "signup" }) {
  const { login, signup } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      await (mode === "login" ? login(email, password) : signup(email, password));
      navigate("/pantry");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Something went wrong.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <main className="auth-page">
      <h1>🫐 BerryMindful</h1>
      <h2>{mode === "login" ? "Log in" : "Create an account"}</h2>
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
        <label>
          Password
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
            minLength={8}
            autoComplete={mode === "login" ? "current-password" : "new-password"}
          />
        </label>
        {error && <p className="error">{error}</p>}
        <button type="submit" disabled={submitting}>
          {submitting ? "..." : mode === "login" ? "Log in" : "Sign up"}
        </button>
      </form>
      <p>
        {mode === "login" ? (
          <a href="/signup">Need an account? Sign up</a>
        ) : (
          <a href="/login">Already registered? Log in</a>
        )}
      </p>
      {mode === "login" && (
        <p>
          <a href="/forgot-password">Forgot password?</a>
        </p>
      )}
    </main>
  );
}
