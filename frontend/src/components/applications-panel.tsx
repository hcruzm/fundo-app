"use client";

import { useEffect, useState } from "react";
import { usePathname } from "next/navigation";

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { listApplications, type ApplicationSummary } from "@/lib/api";

type PanelState =
  | { status: "loading" }
  | { status: "error"; message: string }
  | { status: "ready"; applications: ApplicationSummary[] };

const currencyFormatter = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" });
const timestampFormatter = new Intl.DateTimeFormat("en-US", {
  month: "short",
  day: "numeric",
  hour: "numeric",
  minute: "2-digit",
});

export function ApplicationsPanel() {
  const pathname = usePathname();
  const [state, setState] = useState<PanelState>({ status: "loading" });

  useEffect(() => {
    let cancelled = false;

    listApplications()
      .then((applications) => {
        if (!cancelled) {
          setState({ status: "ready", applications });
        }
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          setState({
            status: "error",
            message: error instanceof Error ? error.message : "We could not load the stored applications.",
          });
        }
      });

    return () => {
      cancelled = true;
    };
  }, [pathname]);

  return (
    <Card className="flex h-[28rem] min-h-0 flex-col lg:h-full">
      <CardHeader>
        <CardTitle>Stored applications</CardTitle>
        <CardDescription>Every approved application, most recently updated first.</CardDescription>
      </CardHeader>
      <CardContent className="min-h-0 flex-1 overflow-y-auto">
        {state.status === "loading" && (
          <p className="text-sm text-muted-foreground">Loading applications...</p>
        )}

        {state.status === "error" && <p className="text-sm text-destructive">{state.message}</p>}

        {state.status === "ready" && state.applications.length === 0 && (
          <p className="text-sm text-muted-foreground">
            No applications have been approved yet. Submit the form to see it appear here.
          </p>
        )}

        {state.status === "ready" && state.applications.length > 0 && (
          <ul className="space-y-4">
            {state.applications.map((application) => (
              <li key={application.applicationId} className="border-b pb-4 last:border-b-0 last:pb-0">
                <div className="flex items-baseline justify-between gap-2">
                  <p className="font-medium">
                    {application.firstName} {application.lastName}
                  </p>
                  {application.isReturningCustomer && (
                    <span className="text-xs text-muted-foreground">Updated</span>
                  )}
                </div>
                <p className="text-sm text-muted-foreground">{application.companyName}</p>
                <div className="mt-1 flex items-center justify-between text-sm">
                  <span>{currencyFormatter.format(application.requestedAmount)}</span>
                  <span className="text-muted-foreground">{application.maskedSsn}</span>
                </div>
                <div className="mt-1 flex items-center justify-between text-xs text-muted-foreground">
                  <span>
                    {application.city}, {application.state}
                  </span>
                  <span>{timestampFormatter.format(new Date(application.updatedAt))}</span>
                </div>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}
