import type { Metadata } from "next";
import type { ReactNode } from "react";

import "./globals.css";

export const metadata: Metadata = {
  description: "Weekly billable rollup for the LexTime interview demo.",
  title: "LexTime · Weekly billable rollup",
};

interface RootLayoutProps {
  readonly children: ReactNode;
}

export default function RootLayout({
  children,
}: RootLayoutProps): React.JSX.Element {
  return (
    <html lang="en">
      <body>
        <a className="skip-link" href="#main-content">
          Skip to report
        </a>
        {children}
      </body>
    </html>
  );
}
