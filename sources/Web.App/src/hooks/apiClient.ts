import type { ApiOptions } from "./types/api";

export type HttpMethod = "GET" | "POST";

const buildUrl = <TModel>(options: ApiOptions<TModel>): string => {
  const url = new URL(options.serviceUrl, window.location.origin);
  if (options.params) {
    Object.entries(options.params).forEach(([key, value]) => {
      url.searchParams.append(key, String(value));
    });
  }
  return url.toString();
};

/** Parse the response body as JSON, tolerating an empty body (e.g. 204). */
const parseBody = async (response: Response): Promise<unknown> => {
  const text = await response.text();
  return text ? JSON.parse(text) : null;
};

/**
 * Perform a fetch request and return the parsed body. Throws on a non-2xx
 * response or an aborted request; callers decide how to handle failures.
 */
export const sendRequest = async <TModel>(
  method: HttpMethod,
  options: ApiOptions<TModel>,
): Promise<TModel | null> => {
  const response = await fetch(buildUrl(options), {
    method,
    headers: { "Content-Type": "application/json" },
    body: method === "POST" ? JSON.stringify(options.body) : undefined,
    signal: options.signal,
  });

  if (!response.ok) {
    throw new Error(`HTTP ${response.status} ${response.statusText}`);
  }

  return (await parseBody(response)) as TModel | null;
};
