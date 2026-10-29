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

export const accountUrls = {
  passwordRules: "/api/account/password-rules",
  register: "/api/account/register",
} as const;
