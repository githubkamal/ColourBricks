import { apiBaseUrl } from "@/lib/config";
import { ApiError, apiClient } from "@/lib/api";

export interface Attachment {
  id: number;
  ownerType: string;
  ownerId: number;
  originalFileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedAtUtc: string;
  uploadedByUserId: number | null;
}

export function listAttachments(ownerType: string, ownerId: number): Promise<Attachment[]> {
  return apiClient.get<Attachment[]>(
    `/attachments?ownerType=${encodeURIComponent(ownerType)}&ownerId=${ownerId}`,
  );
}

export async function uploadAttachment(
  ownerType: string,
  ownerId: number,
  file: File,
): Promise<Attachment> {
  const body = new FormData();
  body.set("ownerType", ownerType);
  body.set("ownerId", String(ownerId));
  body.set("file", file);

  const response = await fetch(`${apiBaseUrl}/attachments`, {
    method: "POST",
    credentials: "include",
    body,
  });

  if (!response.ok) {
    let detail = `Upload failed (${response.status})`;
    try {
      const problem = (await response.json()) as { detail?: string };
      if (problem.detail) detail = problem.detail;
    } catch {
      /* keep the default */
    }
    throw new ApiError(response.status, detail);
  }

  return (await response.json()) as Attachment;
}

/** The download URL for an attachment (the API enforces the owner's view permission). */
export function attachmentUrl(id: number): string {
  return `${apiBaseUrl}/attachments/${id}`;
}
