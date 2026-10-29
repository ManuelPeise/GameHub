import React from "react";
import type { ApiOptions, ApiResult } from "./types/api";
import { sendRequest } from "./apiClient";

export const useStatefulApi = <TModel>(
  options: ApiOptions<TModel>,
): ApiResult<TModel> => {
  const optionsRef = React.useRef(options);
  React.useEffect(() => {
    optionsRef.current = options;
  }, [options]);

  const [isDataBound, setIsDataBound] = React.useState(false);
  const [isLoading, setIsLoading] = React.useState(false);
  const [data, setData] = React.useState<TModel | null>(null);
  const [error, setError] = React.useState<unknown | null>(null);

  const request = React.useCallback(
    async (method: "GET" | "POST", options?: ApiOptions<TModel>) => {
      const merged: ApiOptions<TModel> = {
        ...optionsRef.current,
        ...options,
      };

      setIsLoading(true);
      setError(null);
      try {
        setData(await sendRequest<TModel>(method, merged));
        setIsDataBound(true);
      } catch (err) {
        // A caller-triggered abort is expected, not an error to surface.
        if (err instanceof DOMException && err.name === "AbortError") {
          return;
        }
        console.error(err);
        setError(err);
      } finally {
        setIsLoading(false);
      }
    },
    [],
  );

  const get = React.useCallback(
    (options?: ApiOptions<TModel>) => request("GET", options),
    [request],
  );

  const post = React.useCallback(
    (options?: ApiOptions<TModel>) => request("POST", options),
    [request],
  );

  const rebind = React.useCallback(
    (options?: ApiOptions<TModel>) => get(options),
    [get],
  );

  return {
    data,
    isDataBound,
    isLoading,
    isError: error !== null,
    error,
    get,
    post,
    rebind,
  };
};
