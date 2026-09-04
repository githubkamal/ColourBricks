import { VendorStatementPage } from "@/features/vendor-payments/vendor-statement-page";

export default function Page() {
  return (
    <VendorStatementPage
      partyType="FieldOfficer"
      title="Field Officer Statement"
      pickerLabel="Field officer"
    />
  );
}
