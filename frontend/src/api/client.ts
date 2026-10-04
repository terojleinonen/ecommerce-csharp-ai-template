const BASE_URL = (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? '';

/** RFC 9457 problem details as returned by the API. */
export interface ProblemDetails {
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  errors?: Record<string, string[]>;
}

export class ApiError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails;

  constructor(status: number, problem: ProblemDetails) {
    super(problem.detail ?? problem.title ?? `Request failed with status ${status}`);
    this.name = 'ApiError';
    this.status = status;
    this.problem = problem;
  }

  /** Flattened field errors for forms, keyed by camelCase field name. */
  get fieldErrors(): Record<string, string> {
    const result: Record<string, string> = {};
    for (const [key, messages] of Object.entries(this.problem.errors ?? {})) {
      const field = key.charAt(0).toLowerCase() + key.slice(1);
      if (messages[0]) result[field] = messages[0];
    }
    return result;
  }
}

let accessToken: string | null = null;
let onUnauthorized: (() => void) | null = null;

export function setAccessToken(token: string | null) {
  accessToken = token;
}

export function setUnauthorizedHandler(handler: (() => void) | null) {
  onUnauthorized = handler;
}

type Query = Record<string, string | number | boolean | undefined | null>;

export function buildQuery(params: Query = {}): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === null || value === '') continue;
    search.set(key, String(value));
  }
  const qs = search.toString();
  return qs ? `?${qs}` : '';
}

export async function apiRequest<T>(
  path: string,
  options: { method?: string; body?: unknown; query?: Query; signal?: AbortSignal } = {},
): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' };
  if (options.body !== undefined) headers['Content-Type'] = 'application/json';
  if (accessToken) headers.Authorization = `Bearer ${accessToken}`;

  let response: Response;
  try {
    response = await fetch(`${BASE_URL}${path}${buildQuery(options.query)}`, {
      method: options.method ?? 'GET',
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
      signal: options.signal,
    });
  } catch (err) {
    if (err instanceof DOMException && err.name === 'AbortError') throw err;
    throw new ApiError(0, { title: 'Network error', detail: 'Could not reach the server. Check your connection.' });
  }

  if (response.status === 401 && accessToken) onUnauthorized?.();

  if (!response.ok) {
    let problem: ProblemDetails = { status: response.status };
    try {
      problem = { ...problem, ...((await response.json()) as ProblemDetails) };
    } catch {
      // Non-JSON error body (e.g. proxy error page).
    }
    if (response.status === 429 && !problem.detail) {
      problem.detail = 'Too many requests. Please wait a moment and try again.';
    }
    throw new ApiError(response.status, problem);
  }

  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}
