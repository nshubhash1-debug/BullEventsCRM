import { z } from "zod";

export const credentialsSchema = z.object({
  email: z.string().trim().min(1, "Email is required").email("Enter a valid email address"),
  // Presence only. The length rule belongs where a password is chosen, not
  // where an existing one is typed back in — enforcing it here just turns a
  // wrong password into a different-looking error.
  password: z.string().min(1, "Password is required"),
  remember: z.boolean(),
});

export type CredentialsValues = z.infer<typeof credentialsSchema>;

export const otpSchema = z.object({
  code: z.string().length(6, "Enter the 6-digit code"),
});

export type OtpValues = z.infer<typeof otpSchema>;
