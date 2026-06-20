import Link from "next/link";
import { notFound } from "next/navigation";
import { Footer } from "@/components/Footer";
import { Header } from "@/components/Header";
import { makeupSteps, services } from "@/lib/data";

type PageProps = {
  params: Promise<{ slug: string }>;
};

export default async function ServiceDetailPage({ params }: PageProps) {
  const { slug } = await params;
  const service = services.find((item) => item.slug === slug);

  if (!service) {
    notFound();
  }

  return (
    <>
      <Header />
      <main className="bg-white">
        <section className="bg-brand-pale py-8">
          <div className="container-beauty text-center">
            <p className="text-[13px] text-brand-muted">Trang chủ / Dịch vụ makeup / {service.title}</p>
            <h1 className="mt-3 font-serif text-[34px] uppercase text-brand-red">{service.title}</h1>
          </div>
        </section>

        <section className="container-beauty grid grid-cols-[0.9fr_1.1fr] gap-10 py-12 max-[900px]:grid-cols-1">
          <div className="overflow-hidden rounded-md bg-brand-soft">
            <img src={service.image} alt={service.title} className="h-[520px] w-full object-cover max-[520px]:h-[340px]" />
          </div>
          <div>
            <h2 className="font-serif text-[30px] uppercase text-brand-red">Quy trình makeup</h2>
            <p className="mt-3 text-[15px] leading-7 text-brand-muted">
              Giá dịch vụ {service.price}. Quy trình được chia rõ từng bước để khách hàng biết trước makeup gồm những gì trước khi đặt lịch.
            </p>
            <div className="mt-7 grid gap-4">
              {makeupSteps.map((step, index) => (
                <article key={step.title} className="grid grid-cols-[44px_1fr] gap-4 rounded border border-brand-line bg-white p-4">
                  <span className="grid h-11 w-11 place-items-center rounded-full bg-brand-red text-[15px] font-bold text-white">{index + 1}</span>
                  <div>
                    <h3 className="text-[16px] font-bold">{step.title}</h3>
                    <p className="mt-2 text-[14px] leading-6 text-brand-muted">{step.text}</p>
                  </div>
                </article>
              ))}
            </div>
            <div className="mt-8 flex gap-4 max-[520px]:grid">
              <Link href="/dat-lich" className="btn-red">Đặt lịch ngay</Link>
              <Link href="/dich-vu-makeup" className="btn-white border-brand-red text-brand-red">Xem dịch vụ khác</Link>
            </div>
          </div>
        </section>
      </main>
      <Footer />
    </>
  );
}
