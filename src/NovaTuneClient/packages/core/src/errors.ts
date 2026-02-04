import type { AxiosError } from 'axios';

/**
 * RFC 7807 Problem Details structure.
 */
export interface ProblemDetails {
  type: string;
  title: string;
  status: number;
  detail?: string;
  instance?: string;
  extensions?: Record<string, unknown>;
}

/**
 * Custom error class for API errors with RFC 7807 Problem Details.
 */
export class ApiError extends Error {
  constructor(
    public readonly problem: ProblemDetails,
    public readonly originalError?: unknown
  ) {
    super(problem.title);
    this.name = 'ApiError';
  }

  /**
   * Extracts the error code from the type URL.
   * For example, "https://novatune.dev/errors/invalid-credentials" returns "invalid-credentials".
   */
  get errorCode(): string {
    const match = this.problem.type.match(/\/errors\/(.+)$/);
    return match?.[1] ?? 'unknown';
  }

  /**
   * Returns true if this is a validation error (HTTP 400).
   */
  get isValidationError(): boolean {
    return this.problem.status === 400;
  }

  /**
   * Returns true if this is an authentication error (HTTP 401).
   */
  get isAuthError(): boolean {
    return this.problem.status === 401;
  }

  /**
   * Returns true if this is a forbidden error (HTTP 403).
   */
  get isForbiddenError(): boolean {
    return this.problem.status === 403;
  }

  /**
   * Returns true if this is a not found error (HTTP 404).
   */
  get isNotFoundError(): boolean {
    return this.problem.status === 404;
  }
}

/**
 * Parses an Axios error into an ApiError.
 * If the response contains RFC 7807 Problem Details, it will be used.
 * Otherwise, a generic error is created.
 */
export function parseApiError(error: AxiosError): ApiError {
  if (error.response?.data && typeof error.response.data === 'object') {
    const data = error.response.data as Partial<ProblemDetails>;
    if (data.type && data.title && data.status) {
      return new ApiError(data as ProblemDetails, error);
    }
  }

  // Create a generic error for non-RFC 7807 responses
  const status = error.response?.status ?? 0;
  let title = 'Network Error';
  let type = 'https://novatune.dev/errors/network-error';

  if (status >= 500) {
    title = 'Server Error';
    type = 'https://novatune.dev/errors/server-error';
  } else if (status === 401) {
    title = 'Authentication Required';
    type = 'https://novatune.dev/errors/unauthorized';
  } else if (status === 403) {
    title = 'Access Denied';
    type = 'https://novatune.dev/errors/forbidden';
  } else if (status === 404) {
    title = 'Not Found';
    type = 'https://novatune.dev/errors/not-found';
  }

  return new ApiError(
    {
      type,
      title,
      status,
      detail: error.message,
    },
    error
  );
}

/**
 * Type guard to check if an error is an ApiError.
 */
export function isApiError(error: unknown): error is ApiError {
  return error instanceof ApiError;
}
