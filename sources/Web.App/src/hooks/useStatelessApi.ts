import React from "react";
import type { ApiOptions, StatelessApiResult } from "./types/api";
import { sendRequest } from "./apiClient";

/**
 * Thin fetch wrapper without internal state: get/post resolve to the parsed
 * body and reject on failure, so the caller owns loading/error handling.
 * Base options passed to the hook are merged with per-call options.
 */
export const useStatelessApi = <TModel>(
  baseOptions?: ApiOptions<TModel>,
): StatelessApiResult<TModel> => {
  const baseRef = React.useRef(baseOptions);
  React.useEffect(() => {
    baseRef.current = baseOptions;
  }, [baseOptions]);

  const merge = React.useCallback(
    (options?: ApiOptions<TModel>): ApiOptions<TModel> => {
      const merged = { ...baseRef.current, ...options };
      if (!merged.serviceUrl) {
        throw new Error("useStatelessApi: serviceUrl is required.");
      }
      return merged as ApiOptions<TModel>;
    },
    [],
  );

  const get = React.useCallback(
    (options?: ApiOptions<TModel>) => sendRequest<TModel>("GET", merge(options)),
    [merge],
  );

  const post = React.useCallback(
    (options?: ApiOptions<TModel>) =>
      sendRequest<TModel>("POST", merge(options)),
    [merge],
  );

  return { get, post };
};
