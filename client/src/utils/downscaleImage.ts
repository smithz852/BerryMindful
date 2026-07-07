const MAX_LONG_EDGE = 1500;
const JPEG_QUALITY = 0.8;

// Phone cameras produce 12MP+ images; OCR doesn't need them. Resizing before
// upload cuts upload time, Vision latency, and cost.
export async function downscaleImage(file: File): Promise<Blob> {
  const bitmap = await createImageBitmap(file);
  const longEdge = Math.max(bitmap.width, bitmap.height);

  if (longEdge <= MAX_LONG_EDGE) {
    bitmap.close();
    return file;
  }

  const scale = MAX_LONG_EDGE / longEdge;
  const canvas = document.createElement("canvas");
  canvas.width = Math.round(bitmap.width * scale);
  canvas.height = Math.round(bitmap.height * scale);

  const ctx = canvas.getContext("2d");
  if (!ctx) {
    bitmap.close();
    return file;
  }
  ctx.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
  bitmap.close();

  return new Promise((resolve) => {
    canvas.toBlob(
      (blob) => resolve(blob ?? file),
      "image/jpeg",
      JPEG_QUALITY,
    );
  });
}
