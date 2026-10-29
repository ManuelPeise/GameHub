import type { ApiOptions } from "./types/api";

export type HttpMethod = "GET" | "POST";

/** Thrown for every non-2xx response; carries the status code and the parsed response body. */
export class ApiError extends Error {
  readonly status: number;
  readonly body: unknown;

  constructor(status: number, statusText: string, body: unknown) {
    super(`HTTP ${status} ${statusText}`);
    this.name = "ApiError";
    this.status = status;
    this.body = body;
  }
}

/** Field errors of an ASP.NET Core validation problem (status 400), e.g. { email: ["..."] }. */
export const getValidationErrors = (error: unknown): Record<string, string[]> => {
  if (!(error instanceof ApiError) || error.status !== 400) {
    return {};
  }
  const body = error.body as { errors?: Record<string, string[]> } | null;
  return body?.errors ?? {};
};

const buildUrl = <TModel, TBody>(options: ApiOptions<TModel, TBody>): string => {
  const url = new URL(options.serviceUrl, window.location.origin);
  if (options.params) {
    Object.entries(options.params).forEach(([key, value]) => {
      url.searchParams.append(key, String(value));
    });
  }
  return url.toString();
};

/** Parse the response body as JSON, tolerating an empty or non-JSON body. */
const parseBody = async (response: Response): Promise<unknown> => {
  const text = await response.text();
  if (!text) {
    return null;
  }
  try {
    return JSON.parse(text);
  } catch {
    return text;
  }
};

/**
 * Perform a fetch request and return the parsed body. Throws ApiError on a non-2xx
 * response and rethrows aborts; callers decide how to handle failures.
 */
export const sendRequest = async <TModel, TBody = TModel>(
  method: HttpMethod,
  options: ApiOptions<TModel, TBody>,
): Promise<TModel | null> => {
  const response = await fetch(buildUrl(options), {
    method,
    headers: { "Content-Type": "application/json" },
    body: method === "POST" ? JSON.stringify(options.body) : undefined,
    signal: options.signal,
  });

  const body = await parseBody(response);

  if (!response.ok) {
    throw new ApiError(response.status, response.statusText, body);
  }

  return body as TModel | null;
};
