import { useState, useMemo, useEffect } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import AccountPanel from "../../components/AccountPanel";
import AccountDropdown from "../../components/account/AccountDropdown";

type EventItem = {
  id: number;
  title: string;
  date: string;
  place: string;
  price: string;
  category: string;
  image?: string;
  featured?: boolean;
};


const categories = [
  { label: "Âm nhạc", icon: "♫" },
  { label: "Sân khấu", icon: "✦" },
  { label: "Thể thao", icon: "⚽" },
  { label: "Hội thảo", icon: "◫" },
  { label: "Điện ảnh", icon: "▶" },
  { label: "Workshop", icon: "✎" },
];

const events: EventItem[] = [
  {
    id: 1,
    title: "NEON PULSE — Live in Saigon",
    date: "20:00 · 29.08.2026",
    place: "The Global City, TP. Hồ Chí Minh",
    price: "Từ 650.000đ",
    category: "Âm nhạc",
    featured: true,
  },
  {
    id: 2,
    title: "Giai điệu mùa thu — Chamber Night",
    date: "19:30 · 05.09.2026",
    place: "Nhà hát Thành phố, TP. Hồ Chí Minh",
    price: "Từ 450.000đ",
    category: "Âm nhạc",
  },
  {
    id: 3,
    title: "Vietnam Innovation Forum 2026",
    date: "08:30 · 12.09.2026",
    place: "Thiskyhall Sala, TP. Hồ Chí Minh",
    price: "Từ 300.000đ",
    category: "Hội thảo",
  },
  {
    id: 4,
    title: "Đường chạy thành phố — Night Run",
    date: "18:00 · 19.09.2026",
    place: "Công viên Bờ sông Sài Gòn",
    price: "Từ 390.000đ",
    category: "Thể thao",
  },
  {
    id: 5,
    title: "Mộng Trăng — Kịch đương đại",
    date: "20:00 · 26.09.2026",
    place: "Nhà hát Thanh Niên, Hà Nội",
    price: "Từ 280.000đ",
    category: "Sân khấu",
  },
  {
    id: 6,
    title: "Bếp Việt mới — Culinary Workshop",
    date: "09:00 · 03.10.2026",
    place: "The Sentry, TP. Hồ Chí Minh",
    price: "Từ 520.000đ",
    category: "Workshop",
  },
  {
    id: 7,
    title: "Afterglow — Indie Music Festival",
    date: "17:30 · 10.10.2026",
    place: "Vinhomes Grand Park, TP. Hồ Chí Minh",
    price: "Từ 590.000đ",
    category: "Âm nhạc",
  },
  {
    id: 8,
    title: "Future Makers — Creative Conference",
    date: "08:00 · 17.10.2026",
    place: "GEM Center, TP. Hồ Chí Minh",
    price: "Từ 420.000đ",
    category: "Hội thảo",
  },
  {
    id: 9,
    title: "Bên Kia Màu Sắc — Triển lãm tương tác",
    date: "09:00 · 24.10.2026",
    place: "The Factory Contemporary Arts Centre",
    price: "Từ 180.000đ",
    category: "Điện ảnh",
  },
  {
    id: 10,
    title: "Thở — Wellness & Yoga Day",
    date: "06:30 · 31.10.2026",
    place: "Thảo Điền Riverside, TP. Hồ Chí Minh",
    price: "Từ 250.000đ",
    category: "Workshop",
  },
  {
    id: 11,
    title: "Taste of Vietnam — Food Weekend",
    date: "10:00 · 07.11.2026",
    place: "Công viên 23 Tháng 9, TP. Hồ Chí Minh",
    price: "Miễn phí",
    category: "Workshop",
  },
  {
    id: 12,
    title: "Đêm Kể Chuyện — Sân khấu thử nghiệm",
    date: "20:00 · 14.11.2026",
    place: "Nhà hát 5B, TP. Hồ Chí Minh",
    price: "Từ 220.000đ",
    category: "Sân khấu",
  },
];


const heroSlides = [
  { event: events[0], kicker: "Lựa chọn của EventGo", headline: "Âm nhạc chạm vào\nmọi giác quan.", note: "NEON PULSE mang sân khấu ánh sáng đỉnh cao đến Sài Gòn trong một đêm duy nhất.", shortDate: "29.08" },
  { event: events[3], kicker: "Sự kiện thể thao", headline: "Thành phố thức giấc\ncùng từng bước chạy.", note: "Một cung đường đêm rực sáng, nơi năng lượng và nhịp sống Sài Gòn cùng bứt tốc.", shortDate: "19.09" },
  { event: events[2], kicker: "Tư duy dẫn lối", headline: "Ý tưởng hôm nay.\nTương lai ngày mai.", note: "Gặp gỡ những nhà sáng tạo, doanh nhân và chuyên gia công nghệ đang định hình Việt Nam mới.", shortDate: "12.09" },
];

function Icon({ name }: { name: "search" | "ticket" | "user" | "heart" | "arrow" | "pin" }) {
  const paths = {
    search: <><circle cx="11" cy="11" r="7" /><path d="m20 20-4-4" /></>,
    ticket: <><path d="M4 5h16v4a3 3 0 0 0 0 6v4H4v-4a3 3 0 0 0 0-6V5Z" /><path d="M12 7v10" /></>,
    user: <><circle cx="12" cy="8" r="4" /><path d="M4.5 21a7.5 7.5 0 0 1 15 0" /></>,
    heart: <path d="M20.8 4.6a5.5 5.5 0 0 0-7.8 0L12 5.7l-1.1-1.1a5.5 5.5 0 0 0-7.8 7.8l1.1 1.1L12 21l7.8-7.5 1.1-1.1a5.5 5.5 0 0 0-.1-7.8Z" />,
    arrow: <><path d="M5 12h14" /><path d="m13 6 6 6-6 6" /></>,
    pin: <><path d="M20 10c0 5-8 11-8 11S4 15 4 10a8 8 0 1 1 16 0Z" /><circle cx="12" cy="10" r="2" /></>,
  };
  return <svg aria-hidden="true" className="icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">{paths[name]}</svg>;
}

function RenderEventImage({ image, title, category }: { image?: string; title: string; category?: string }) {
  if (image && image.trim() !== "") {
    return <img src={image} alt={title} />;
  }
  return (
    <div className="img-fallback-placeholder">
      <span className="placeholder-symbol">✦</span>
      <span>{category ?? "EventGo"}</span>
    </div>
  );
}

export const HomePage = () => {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const urlQuery = searchParams.get("q") || "";
  const [query, setQuery] = useState(urlQuery);

  useEffect(() => {
    setQuery(urlQuery);
  }, [urlQuery]);
  const [activeCategory, setActiveCategory] = useState("Tất cả");
  const [liked, setLiked] = useState<number[]>([]);
  const [heroIndex, setHeroIndex] = useState(0);
  const [selectedEvent, setSelectedEvent] = useState<EventItem | null>(null);
  const [accountOpen, setAccountOpen] = useState(false);
  const [accountName, setAccountName] = useState<string | null>(null);
  const [salesTab, setSalesTab] = useState<"week" | "month">("week");

  const hero = heroSlides[heroIndex];
  const saleEvents = salesTab === "week" ? events.slice(6, 10) : events.slice(8, 12);

  const visibleEvents = useMemo(() => events.filter((event) => {
    const categoryMatch = activeCategory === "Tất cả" || event.category === activeCategory;
    const searchMatch = `${event.title} ${event.place}`.toLowerCase().includes(query.toLowerCase());
    return categoryMatch && searchMatch;
  }), [query, activeCategory]);

  const toggleLike = (id: number) => setLiked((current) => current.includes(id) ? current.filter((item) => item !== id) : [...current, id]);
  const selectCategory = (label: string) => {
    setActiveCategory(activeCategory === label ? "Tất cả" : label);
    requestAnimationFrame(() => document.getElementById("events")?.scrollIntoView({ behavior: "smooth", block: "start" }));
  };

  return (
    <div className="home-container">
      {/* Category Strip */}
      <nav className="category-nav" aria-label="Danh mục sự kiện">
        <div className="category-strip shell">
          {categories.map((category) => (
            <button key={category.label} className={activeCategory === category.label ? "active" : ""} onClick={() => selectCategory(category.label)}>
              <span>{category.icon}</span>{category.label}
            </button>
          ))}
        </div>
      </nav>

      {/* Hero Section */}
      <section className="hero" id="top">
        {hero.event.image ? (
          <img key={hero.event.id} src={hero.event.image} alt={hero.event.title} />
        ) : (
          <div className="hero-fallback-bg" />
        )}
        <div className="hero-shade" />
        <div className="hero-content shell">
          <div className="eyebrow"><span /> {hero.kicker}</div>
          <h1>{hero.headline}</h1>
          <p>{hero.note}</p>
          <div className="hero-actions">
            <a className="button primary" href="#events">Khám phá ngay <Icon name="arrow" /></a>
            <span><b>{hero.shortDate}</b><small>{hero.event.place}</small></span>
          </div>
          <div className="hero-dots" aria-label="Chọn banner">
            {heroSlides.map((slide, index) => <button key={slide.event.id} aria-label={`Banner ${index + 1}`} className={heroIndex === index ? "active" : ""} onClick={() => setHeroIndex(index)} />)}
          </div>
        </div>
        <span className="hero-index">0{heroIndex + 1} / 03</span>
      </section>

      {/* Discovery Section */}
      <section className="discovery-section">
        <div className="shell">

          <div className="mini-section-head event-row-title">
            <div><span>✦</span><h2>Sự kiện đặc biệt</h2></div>
            <a href="#events">Khám phá thêm <Icon name="arrow" /></a>
          </div>
          <div className="poster-rail">
            {[events[4], events[6], events[8], events[9], events[11]].map((event) => (
              <article key={event.id} onClick={() => setSelectedEvent(event)}>
                <div>
                  <RenderEventImage image={event.image} title={event.title} category={event.category} />
                  <span>{event.category}</span>
                </div>
                <p>{event.date}</p>
                <h3>{event.title}</h3>
              </article>
            ))}
          </div>

          <div className="mini-section-head event-row-title">
            <div><span>●</span><h2>Đang thịnh hành</h2></div>
            <span className="live-badge">CẬP NHẬT TRỰC TIẾP</span>
          </div>
          <div className="trending-grid">
            {[events[0], events[7], events[3], events[10]].map((event, index) => (
              <button key={event.id} onClick={() => setSelectedEvent(event)}>
                <b>0{index + 1}</b>
                <RenderEventImage image={event.image} title={event.title} category={event.category} />
                <span><small>{event.category}</small><strong>{event.title}</strong><em>{event.price}</em></span>
              </button>
            ))}
          </div>
        </div>
      </section>

      {/* All Events Grid */}
      <section className="section shell" id="events">
        <div className="section-head">
          <div><p className="kicker">ĐANG ĐƯỢC QUAN TÂM</p><h2>Sự kiện nổi bật</h2></div>
          <button className="text-link" onClick={() => { setActiveCategory("Tất cả"); setQuery(""); }}>Xem tất cả <Icon name="arrow" /></button>
        </div>
        <div className="event-grid">
          {visibleEvents.slice(0, 4).map((event) => (
            <article className="event-card" key={event.id}>
              <div className="event-image">
                <RenderEventImage image={event.image} title={event.title} category={event.category} />
                <span className="event-tag">{event.category}</span>
                <button onClick={() => toggleLike(event.id)} className={liked.includes(event.id) ? "liked" : ""} aria-label="Lưu sự kiện"><Icon name="heart" /></button>
              </div>
              <div className="event-copy">
                <p className="date">{event.date}</p>
                <h3>{event.title}</h3>
                <p className="place"><Icon name="pin" />{event.place}</p>
                <div><strong>{event.price}</strong><button className="card-arrow" onClick={() => setSelectedEvent(event)} aria-label={`Xem ${event.title}`}><Icon name="arrow" /></button></div>
              </div>
            </article>
          ))}
        </div>
        {visibleEvents.length === 0 && <div className="empty-state"><span>⌕</span><h3>Chưa tìm thấy sự kiện</h3><p>Thử từ khóa khác hoặc chọn lại danh mục.</p></div>}
      </section>

      {/* Selected Event Modal */}
      {selectedEvent && (
        <div className="modal-backdrop" role="presentation" onMouseDown={() => setSelectedEvent(null)}>
          <section className="event-modal" role="dialog" aria-modal="true" aria-label="Chi tiết sự kiện" onMouseDown={(event) => event.stopPropagation()}>
            <button className="modal-close" onClick={() => setSelectedEvent(null)} aria-label="Đóng">×</button>
            <div className="modal-image-wrapper">
              <RenderEventImage image={selectedEvent.image} title={selectedEvent.title} category={selectedEvent.category} />
            </div>
            <div>
              <p className="kicker">{selectedEvent.category}</p>
              <h2>{selectedEvent.title}</h2>
              <p className="modal-date">{selectedEvent.date}</p>
              <p className="modal-place"><Icon name="pin" />{selectedEvent.place}</p>
              <div className="modal-buy">
                <strong>{selectedEvent.price}</strong>
                <button
                  className="button primary"
                  onClick={() => {
                    setSelectedEvent(null);
                    navigate(`/events/${selectedEvent.id}`);
                  }}
                >
                  Đặt vé ngay <Icon name="arrow" />
                </button>
              </div>
            </div>
          </section>
        </div>
      )}

      {/* Account Slide Panel */}
      <AccountPanel
        open={accountOpen}
        onClose={() => setAccountOpen(false)}
        onUserChange={(name) => {
          setAccountName(name);
          if (name) {
            setSelectedEvent(null);
          }
        }}
        onOpenRegister={() => navigate("/register")}
        initialEmail=""
      />
    </div>
  );
};
