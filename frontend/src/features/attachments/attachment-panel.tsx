"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useRef } from "react";
import { toast } from "sonner";
import { ApiError } from "@/lib/api";
import { attachmentUrl, listAttachments, uploadAttachment } from "./api";

/**
 * Reusable attachment list + uploader (BRD §67). Drop it onto any record —
 * purchases, payments, donations, loans — with its owner type and id.
 */
export function AttachmentPanel({ ownerType, ownerId }: { ownerType: string; ownerId: number }) {
  const queryClient = useQueryClient();
  const inputRef = useRef<HTMLInputElement>(null);

  const { data: files = [] } = useQuery({
    queryKey: ["attachments", ownerType, ownerId],
    queryFn: () => listAttachments(ownerType, ownerId),
  });

  const upload = useMutation({
    mutationFn: (file: File) => uploadAttachment(ownerType, ownerId, file),
    onSuccess: () => {
      toast.success("Attachment uploaded");
      void queryClient.invalidateQueries({ queryKey: ["attachments", ownerType, ownerId] });
      if (inputRef.current) inputRef.current.value = "";
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not upload the file"),
  });

  return (
    <div className="space-y-2">
      <div className="flex items-center gap-2">
        <span className="text-sm font-medium">Attachments</span>
        <input
          ref={inputRef}
          type="file"
          aria-label="Attach a file"
          accept=".pdf,.jpg,.jpeg,.png,.webp,.xlsx,.csv"
          className="text-xs"
          onChange={(e) => {
            const file = e.target.files?.[0];
            if (file) upload.mutate(file);
          }}
        />
      </div>
      {files.length === 0 ? (
        <p className="text-muted-foreground text-xs">No attachments.</p>
      ) : (
        <ul className="text-sm">
          {files.map((f) => (
            <li key={f.id}>
              <a
                className="text-primary underline"
                href={attachmentUrl(f.id)}
                target="_blank"
                rel="noreferrer"
              >
                {f.originalFileName}
              </a>
              <span className="text-muted-foreground"> · {(f.sizeBytes / 1024).toFixed(1)} KB</span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
