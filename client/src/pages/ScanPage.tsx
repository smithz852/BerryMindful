import { useRef, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useScanReceipt } from "../hooks/useReceipts";

export function ScanPage() {
  const navigate = useNavigate();
  const scan = useScanReceipt();
  const inputRef = useRef<HTMLInputElement>(null);
  const [error, setError] = useState<string | null>(null);

  async function handleFile(file: File | undefined) {
    if (!file) return;
    setError(null);
    try {
      const result = await scan.mutateAsync(file);
      navigate("/scan/confirm", { state: result });
    } catch (err) {
      setError(err instanceof Error ? err.message : "Scan failed.");
    }
  }

  return (
    <main className="scan-page">
      <h1>📷 Scan a receipt</h1>
      <p>Take a photo or choose an image of your grocery receipt.</p>

      <input
        ref={inputRef}
        type="file"
        accept="image/*"
        capture="environment"
        hidden
        onChange={(e) => handleFile(e.target.files?.[0])}
      />
      <button
        className="primary"
        onClick={() => inputRef.current?.click()}
        disabled={scan.isPending}
      >
        {scan.isPending ? "Reading receipt… (a few seconds)" : "Choose photo"}
      </button>

      {error && <p className="error">{error}</p>}
      <p>
        <Link to="/pantry">← Back to pantry</Link>
      </p>
    </main>
  );
}
