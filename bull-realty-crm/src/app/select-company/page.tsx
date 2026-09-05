import type { Metadata } from "next";

import { CompanyChooser } from "@/components/auth/company-chooser";

export const metadata: Metadata = {
  title: "Choose a company",
};

export default function SelectCompanyPage() {
  return <CompanyChooser />;
}
