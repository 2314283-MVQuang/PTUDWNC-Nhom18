import { PageHero } from "@/components/layout/PageHero";

export interface LegalSection {
  heading: string;
  paragraphs: string[];
}

/** Khung chung cho trang Chính sách quyền riêng tư và Điều khoản sử dụng. */
export function LegalPage({ title, description, updated, sections }: {
  title: string;
  description: string;
  updated: string;
  sections: LegalSection[];
}) {
  return (
    <div className="flex flex-col">
      <PageHero title={title} description={description} emojis={["📜", "🔒"]} />
      <article className="container-page max-w-3xl py-10">
        <p className="mb-8 text-sm text-neutral-500">Cập nhật lần cuối: {updated}</p>
        {sections.map((section) => (
          <section key={section.heading} className="mb-8">
            <h2 className="mb-3 text-xl font-bold text-neutral-900">{section.heading}</h2>
            {section.paragraphs.map((text) => (
              <p key={text} className="mb-3 leading-relaxed text-neutral-700">{text}</p>
            ))}
          </section>
        ))}
      </article>
    </div>
  );
}
