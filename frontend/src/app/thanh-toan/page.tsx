import { Footer } from "@/components/Footer";
import { Header } from "@/components/Header";
import { CheckoutForm } from "@/components/CheckoutForm";

export default function CheckoutPage() {
  return (
    <>
      <Header />
      <main className="container-beauty py-12">
        <h1 className="section-title">Thanh toán</h1>
        <CheckoutForm />
      </main>
      <Footer />
    </>
  );
}
