// Contracts of the account endpoints (Shared.Models/Account).
// To be replaced by the types generated from Shared.Models once type generation is set up.

export type RegisterRequest = {
  email: string;
  displayName: string;
  password: string;
};

export type RegisterResponse = {
  message: string;
};

export type PasswordRulesResponse = {
  rules: string[];
};

export type LoginRequest = {
  email: string;
  password: string;
  rememberMe: boolean;
};

export type CurrentUserResponse = {
  id: string;
  email: string;
  displayName: string;
  roles: string[];
};

export type EmailRequest = {
  email: string;
};

export type ResetPasswordRequest = {
  userId: string;
  token: string;
  newPassword: string;
};

export type MessageResponse = {
  message: string;
};

/** Values of the "code" extension of a failed login (Shared.Models LoginErrorCodes). */
export type LoginErrorCode =
  | "invalid_credentials"
  | "locked_out"
  | "email_not_confirmed"
  | "account_disabled";

export const accountUrls = {
  passwordRules: "/api/account/password-rules",
  register: "/api/account/register",
  login: "/api/account/login",
  logout: "/api/account/logout",
  me: "/api/account/me",
  forgotPassword: "/api/account/forgot-password",
  resetPassword: "/api/account/reset-password",
  resendConfirmation: "/api/account/resend-confirmation",
} as const;

/** Page paths of Web.App (path-based selection in pages/App.tsx). */
export const pagePaths = {
  home: "/",
  login: "/login",
  register: "/register",
  forgotPassword: "/forgot-password",
  resetPassword: "/reset-password",
  emailConfirmed: "/email-confirmed",
} as const;
