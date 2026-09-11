import { VendorsPage } from "@/features/parties/vendors-page";

export default function Page() {
  return (
    <VendorsPage
      partyType="FieldOfficer"
      title="Field Officers"
      statementHref="/field-officers/statements"
    />
  );
}
