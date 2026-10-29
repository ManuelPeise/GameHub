export type ApiParams = Record<string, string | number | boolean>;

/**
 * Options of a request. TModel is the response type, TBody the request body type
 * (defaults to TModel for endpoints that send and receive the same model).
 */
export type ApiOptions<TModel, TBody = TModel> = {
  /** Absolute URL, or relative to window.location.origin. */
  serviceUrl: string;
  params?: ApiParams;
  body?: TBody;
  signal?: AbortSignal;
  /** Phantom marker so TModel stays part of the type. */
  readonly __model?: TModel;
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
