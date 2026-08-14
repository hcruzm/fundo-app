import type { ApplicationPayload } from "@/lib/schema";

const baseUrl = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5080";

export type SubmitResult =
  | { decision: "Approved"; applicationId: string; customerId: string; isReturningCustomer: boolean }
  | { decision: "Denied"; reason: string };

export type ApplicationDetail = {
  applicationId: string;
  customerId: string;
  requestedAmount: number;
  status: string;
  firstName: string;
  lastName: string;
  companyName: string;
  maskedSsn: string;
  address: { street: string; city: string; state: string; postalCode: string };
  createdAt: string;
  updatedAt: string;
};

export async function submitApplication(payload: ApplicationPayload): Promise<SubmitResult> {
  const response = await fetch(`${baseUrl}/api/applications`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });

  if (!response.ok) {
    const problem = await response.json().catch(() => null);
    throw new Error(problem?.title ?? "We could not submit your application. Please try again.");
  }

  return (await response.json()) as SubmitResult;
}

export async function getApplication(id: string): Promise<ApplicationDetail | null> {
  const response = await fetch(`${baseUrl}/api/applications/${id}`, { cache: "no-store" });

  if (response.status === 404) {
    return null;
  }

  if (!response.ok) {
    throw new Error("We could not load this application.");
  }

  return (await response.json()) as ApplicationDetail;
}
