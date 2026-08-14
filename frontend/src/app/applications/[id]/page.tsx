import Link from "next/link";
import { notFound } from "next/navigation";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { getApplication } from "@/lib/api";

export default async function ApplicationPage({
  params,
  searchParams,
}: {
  params: Promise<{ id: string }>;
  searchParams: Promise<{ returning?: string }>;
}) {
  const { id } = await params;
  const { returning } = await searchParams;
  const application = await getApplication(id);

  if (!application) {
    notFound();
  }

  const amount = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" })
    .format(application.requestedAmount);

  return (
    <main className="flex min-h-screen items-center justify-center p-6">
      <Card className="w-full max-w-xl">
        <CardHeader>
          <CardTitle>Your application was approved</CardTitle>
          <CardDescription>
            {returning
              ? `We updated your existing application. Reference ${application.applicationId}`
              : `Reference ${application.applicationId}`}
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <dl className="grid gap-3 text-sm sm:grid-cols-2">
            <div>
              <dt className="text-muted-foreground">Applicant</dt>
              <dd>{application.firstName} {application.lastName}</dd>
            </div>
            <div>
              <dt className="text-muted-foreground">Company</dt>
              <dd>{application.companyName}</dd>
            </div>
            <div>
              <dt className="text-muted-foreground">Requested amount</dt>
              <dd>{amount}</dd>
            </div>
            <div>
              <dt className="text-muted-foreground">SSN</dt>
              <dd>{application.maskedSsn}</dd>
            </div>
            <div className="sm:col-span-2">
              <dt className="text-muted-foreground">Address</dt>
              <dd>
                {application.address.street}, {application.address.city}, {application.address.state}{" "}
                {application.address.postalCode}
              </dd>
            </div>
          </dl>
          <Button asChild variant="outline">
            <Link href="/">Submit another application</Link>
          </Button>
        </CardContent>
      </Card>
    </main>
  );
}
