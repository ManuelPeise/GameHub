export type ApiParams = Record<string, string | number | boolean>;

export type ApiOptions<TModel> = {
  /** Absolute URL, or relative to window.location.origin. */
  serviceUrl: string;
  params?: ApiParams;
  body?: TModel;
  signal?: AbortSignal;
};

export type StatelessApiResult<TModel> = {
  get: (options?: ApiOptions<TModel>) => Promise<TModel | null>;
  post: (options?: ApiOptions<TModel>) => Promise<TModel | null>;
};

export type ApiResult<TModel> = {
  data: TModel | null;
  isDataBound: boolean;
  isLoading: boolean;
  isError: boolean;
  error: unknown | null;
  get: (options?: ApiOptions<TModel>) => Promise<void>;
  post: (options?: ApiOptions<TModel>) => Promise<void>;
  rebind: (options?: ApiOptions<TModel>) => Promise<void>;
};
