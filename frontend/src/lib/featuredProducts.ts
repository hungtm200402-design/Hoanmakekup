export type FeaturedProduct = {
  id: string;
  slug: string;
  name: string;
  price: number;
  salePrice: number | null;
  stock: number;
  imagePath: string;
};

const assetBase = "/images/products/hoan_doan_mau_make_san_pham_va_banner/02_anh_san_pham";

export const featuredProducts: FeaturedProduct[] = [
  {
    id: "perfect-diary",
    slug: "perfect-diary",
    name: "Kem nen Perfect Diary",
    price: 350000,
    salePrice: null,
    stock: 67,
    imagePath: `${assetBase}/13_combo_trang_diem_tu_nhien.png`
  },
  {
    id: "son-black-rouge-air-fit",
    slug: "son-black-rouge-air-fit",
    name: "Son Black Rouge Air Fit",
    price: 280000,
    salePrice: 250000,
    stock: 89,
    imagePath: `${assetBase}/15_combo_trang_diem_toan_dien.png`
  },
  {
    id: "innisfree-powder",
    slug: "innisfree-powder",
    name: "Phan phu Innisfree",
    price: 320000,
    salePrice: null,
    stock: 45,
    imagePath: `${assetBase}/14_combo_duong_sang_da.png`
  },
  {
    id: "the-ordinary-serum",
    slug: "the-ordinary-serum",
    name: "Serum The Ordinary",
    price: 310000,
    salePrice: 280000,
    stock: 38,
    imagePath: `${assetBase}/16_combo_phuc_hoi_cap_am.png`
  },
  {
    id: "maybelline-mascara",
    slug: "maybelline-mascara",
    name: "Mascara Maybelline",
    price: 220000,
    salePrice: null,
    stock: 52,
    imagePath: `${assetBase}/15_combo_trang_diem_toan_dien.png`
  },
  {
    id: "bioderma",
    slug: "bioderma",
    name: "Tay trang Bioderma",
    price: 350000,
    salePrice: null,
    stock: 58,
    imagePath: `${assetBase}/13_combo_trang_diem_tu_nhien.png`
  }
];
