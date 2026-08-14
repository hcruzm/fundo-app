import { z } from "zod";

// Mirrors SubmitApplicationRequestValidator on the backend. The backend stays the
// authority; this copy exists to give the user immediate feedback.
export const applicationSchema = z.object({
  firstName: z.string().trim().min(1, "First name is required").max(100),
  lastName: z.string().trim().min(1, "Last name is required").max(100),
  companyName: z.string().trim().min(1, "Company name is required").max(200),
  requestedAmount: z.coerce
    .number({ message: "Enter an amount" })
    .positive("The amount must be greater than zero")
    .max(10_000_000, "That amount is too large"),
  ssn: z
    .string()
    .refine((value) => (value.match(/\d/g) ?? []).length === 9, "An SSN must contain exactly nine digits"),
  address: z.object({
    street: z.string().trim().min(1, "Street is required").max(200),
    city: z.string().trim().min(1, "City is required").max(100),
    state: z.string().trim().length(2, "Use the two-letter state code"),
    postalCode: z.string().trim().min(1, "ZIP code is required").max(10),
  }),
});

export type ApplicationFormValues = z.input<typeof applicationSchema>;
export type ApplicationPayload = z.output<typeof applicationSchema>;
