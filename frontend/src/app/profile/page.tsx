import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { googleLinkMessages } from "@/components/google-button";
import { ProfileForm } from "@/components/profile-form";
import { Button, Card, Container } from "@/components/ui";
import { authedApiGet } from "@/lib/api";
import { startGoogleLinkAction, updateProfileAction } from "@/lib/auth-actions";
import { googleEnabled } from "@/lib/providers";
import { getCurrentUser } from "@/lib/session";

export const metadata: Metadata = { title: "Mon profil", robots: { index: false } };

interface FullProfile {
  firstName: string;
  lastName: string;
  studentId: string | null;
  university: { id: string } | null;
  faculty: { id: string } | null;
  department: { id: string } | null;
  specialty: { id: string } | null;
  level: { id: string } | null;
}

export default async function ProfilePage({ searchParams }: { searchParams: Promise<{ welcome?: string; google?: string }> }) {
  if (!(await getCurrentUser())) redirect("/login?next=/profile");
  const { welcome, google } = await searchParams;
  const profile = await authedApiGet<FullProfile>("/api/me");
  const googleOn = await googleEnabled();
  const googleLinked = googleOn
    ? (await authedApiGet<{ linked: boolean }>("/api/auth/google/status").catch(() => ({ linked: false }))).linked
    : false;
  const linkMessage = google ? googleLinkMessages[google] : undefined;

  return (
    <Container className="py-12">
      <Card className="mx-auto max-w-lg p-8">
        <h1 className="text-xl font-semibold">
          {welcome ? "Bienvenue ! Complétez votre profil" : "Mon profil académique"}
        </h1>
        <p className="mt-1 text-sm text-ink-muted">
          Votre spécialité et votre niveau personnalisent votre tableau de bord.
        </p>
        <div className="mt-6">
          <ProfileForm
            action={updateProfileAction}
            initial={{
              firstName: profile.firstName,
              lastName: profile.lastName,
              studentId: profile.studentId,
              universityId: profile.university?.id ?? null,
              facultyId: profile.faculty?.id ?? null,
              departmentId: profile.department?.id ?? null,
              specialtyId: profile.specialty?.id ?? null,
              levelId: profile.level?.id ?? null,
            }}
          />
        </div>
      </Card>

      {googleOn ? (
        <Card className="mx-auto mt-6 max-w-lg p-6">
          <h2 className="text-base font-semibold">Connexion avec Google</h2>
          {linkMessage ? (
            <p role="status" className={`mt-3 rounded-md p-3 text-sm ${linkMessage.ok ? "bg-brand-soft text-brand-strong" : "bg-red-50 text-red-800"}`}>
              {linkMessage.text}
            </p>
          ) : null}
          {googleLinked ? (
            <p className="mt-2 text-sm text-ink-muted">Un compte Google est lié : vous pouvez vous connecter avec Google.</p>
          ) : (
            <form action={startGoogleLinkAction} className="mt-3">
              <p className="mb-3 text-sm text-ink-muted">
                Liez votre compte Google pour vous connecter en un clic. Cela ne modifie ni vos droits ni votre mot de passe.
              </p>
              <Button type="submit" variant="secondary">Lier mon compte Google</Button>
            </form>
          )}
        </Card>
      ) : null}
    </Container>
  );
}
