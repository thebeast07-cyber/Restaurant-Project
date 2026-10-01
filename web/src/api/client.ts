const API_BASE_URL = import.meta.env.VITE_API_BASE_URL as string;

export class ApiError extends Error {
  status: number;

  constructor(status: number, message: string) {
    super(message);
    this.status = status;
  }
}

/**
 * Thin fetch wrapper: attaches the JWT (if present) and the API's base URL, and
 * turns a non-2xx response into a thrown ApiError with the backend's own message
 * (every controller in the API returns `{ message: "..." }` on error) rather than a
 * generic "Response not ok".
 */
export async function apiFetch<T>(path: string, options: RequestInit = {}): Promise<T> {
  const token = localStorage.getItem("token");

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...options.headers,
    },
  });

  if (!response.ok) {
    let message = `Request failed with status ${response.status}`;
    try {
      const body = await response.json();
      message = body?.message ?? message;
    } catch {
      // Response body wasn't JSON — keep the generic message.
    }
    throw new ApiError(response.status, message);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

/**
 * Same as apiFetch but for multipart/form-data uploads — deliberately does NOT set
 * Content-Type itself (the browser sets it, including the multipart boundary, only
 * when left unset on a FormData body).
 */
export async function apiUpload<T>(path: string, formData: FormData): Promise<T> {
  const token = localStorage.getItem("token");

  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: "POST",
    headers: token ? { Authorization: `Bearer ${token}` } : {},
    body: formData,
  });

  if (!response.ok) {
    let message = `Request failed with status ${response.status}`;
    try {
      const body = await response.json();
      message = body?.message ?? message;
    } catch {
      // Response body wasn't JSON — keep the generic message.
    }
    throw new ApiError(response.status, message);
  }

  return (await response.json()) as T;
}
