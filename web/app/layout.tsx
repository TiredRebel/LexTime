import type { Metadata } from "next";
import type { ReactNode } from "react";

import "./globals.css";

export const metadata: Metadata = {
  title: "LexTime",
  description: "Time entries, party directories, and weekly billable rollup for the LexTime interview demo.",
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
          Skip to content
        </a>
        {children}
      </body>
    </html>
  );
}
