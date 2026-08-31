import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { ProfileForm } from "@/components/profile-form";
import { Card, Container } from "@/components/ui";
import { authedApiGet } from "@/lib/api";
import { updateProfileAction } from "@/lib/auth-actions";
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

export default async function ProfilePage({ searchParams }: { searchParams: Promise<{ welcome?: string }> }) {
  if (!(await getCurrentUser())) redirect("/login?next=/profile");
  const { welcome } = await searchParams;
  const profile = await authedApiGet<FullProfile>("/api/me");

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
    </Container>
  );
}
