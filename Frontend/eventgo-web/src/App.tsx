import AccountPanel from "./components/AccountPanel";
import { useMemo, useState } from "react";

type EventItem = {
  id: number;
  title: string;
  date: string;
  place: string;
  price: string;
  category: string;
  image: string;
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
    image: "https://images.unsplash.com/photo-1540039297186-746086ab730b?auto=format&fit=crop&w=1400&q=85",
    featured: true,
  },
  {
    id: 2,
    title: "Giai điệu mùa thu — Chamber Night",
    date: "19:30 · 05.09.2026",
    place: "Nhà hát Thành phố, TP. Hồ Chí Minh",
    price: "Từ 450.000đ",
    category: "Âm nhạc",
    image: "https://images.unsplash.com/photo-1509824227185-9c5a01ceba0d?auto=format&fit=crop&w=1000&q=80",
  },
  {
    id: 3,
    title: "Vietnam Innovation Forum 2026",
    date: "08:30 · 12.09.2026",
    place: "Thiskyhall Sala, TP. Hồ Chí Minh",
    price: "Từ 300.000đ",
    category: "Hội thảo",
    image: "https://images.unsplash.com/photo-1540575467063-178a50c2df87?auto=format&fit=crop&w=1000&q=80",
  },
  {
    id: 4,
    title: "Đường chạy thành phố — Night Run",
    date: "18:00 · 19.09.2026",
    place: "Công viên Bờ sông Sài Gòn",
    price: "Từ 390.000đ",
    category: "Thể thao",
    image: "https://images.unsplash.com/photo-1682367905664-e36b30f15b19?auto=format&fit=crop&w=1000&q=80",
  },
  {
    id: 5,
    title: "Mộng Trăng — Kịch đương đại",
    date: "20:00 · 26.09.2026",
    place: "Nhà hát Thanh Niên, Hà Nội",
    price: "Từ 280.000đ",
    category: "Sân khấu",
    image: "https://images.unsplash.com/photo-1503095396549-807759245b35?auto=format&fit=crop&w=1000&q=80",
  },
  {
    id: 6,
    title: "Bếp Việt mới — Culinary Workshop",
    date: "09:00 · 03.10.2026",
    place: "The Sentry, TP. Hồ Chí Minh",
    price: "Từ 520.000đ",
    category: "Workshop",
    image: "https://images.unsplash.com/photo-1556910103-1c02745aae4d?auto=format&fit=crop&w=1000&q=80",
  },
  {
    id: 7,
    title: "Afterglow — Indie Music Festival",
    date: "17:30 · 10.10.2026",
    place: "Vinhomes Grand Park, TP. Hồ Chí Minh",
    price: "Từ 590.000đ",
    category: "Âm nhạc",
    image: "https://images.unsplash.com/photo-1501386761578-eac5c94b800a?auto=format&fit=crop&w=1000&q=80",
  },
  {
    id: 8,
    title: "Future Makers — Creative Conference",
    date: "08:00 · 17.10.2026",
    place: "GEM Center, TP. Hồ Chí Minh",
    price: "Từ 420.000đ",
    category: "Hội thảo",
    image: "https://images.unsplash.com/photo-1517245386807-bb43f82c33c4?auto=format&fit=crop&w=1000&q=80",
  },
  {
    id: 9,
    title: "Bên Kia Màu Sắc — Triển lãm tương tác",
    date: "09:00 · 24.10.2026",
    place: "The Factory Contemporary Arts Centre",
    price: "Từ 180.000đ",
    category: "Điện ảnh",
    image: "https://images.unsplash.com/photo-1577083552431-6e5fd01aa342?auto=format&fit=crop&w=1000&q=80",
  },
  {
    id: 10,
    title: "Thở — Wellness & Yoga Day",
    date: "06:30 · 31.10.2026",
    place: "Thảo Điền Riverside, TP. Hồ Chí Minh",
    price: "Từ 250.000đ",
    category: "Workshop",
    image: "https://images.unsplash.com/photo-1599447421416-3414500d18a5?auto=format&fit=crop&w=1000&q=80",
  },
  {
    id: 11,
    title: "Taste of Vietnam — Food Weekend",
    date: "10:00 · 07.11.2026",
    place: "Công viên 23 Tháng 9, TP. Hồ Chí Minh",
    price: "Miễn phí",
    category: "Workshop",
    image: "https://images.unsplash.com/photo-1504674900247-0877df9cc836?auto=format&fit=crop&w=1000&q=80",
  },
  {
    id: 12,
    title: "Đêm Kể Chuyện — Sân khấu thử nghiệm",
    date: "20:00 · 14.11.2026",
    place: "Nhà hát 5B, TP. Hồ Chí Minh",
    price: "Từ 220.000đ",
    category: "Sân khấu",
    image: "https://images.unsplash.com/photo-1507676184212-d03ab07a01bf?auto=format&fit=crop&w=1000&q=80",
  },
];

const featuredPeople = [
  { name: "Mỹ Anh", role: "Singer · Songwriter", eventId: 1, image: "https://images.unsplash.com/photo-1494790108377-be9c29b29330?auto=format&fit=crop&w=400&q=80" },
  { name: "Minh", role: "Indie artist", eventId: 7, image: "https://images.unsplash.com/photo-1500648767791-00dcc994a43e?auto=format&fit=crop&w=400&q=80" },
  { name: "Duy Anh", role: "Creative director", eventId: 8, image: "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?auto=format&fit=crop&w=400&q=80" },
  { name: "An Nhiên", role: "Theatre maker", eventId: 12, image: "https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&w=400&q=80" },
  { name: "Lumi Studio", role: "Visual arts", eventId: 9, image: "https://images.unsplash.com/photo-1531058020387-3be344556be6?auto=format&fit=crop&w=400&q=80" },
  { name: "Bếp Nhà Mình", role: "Culinary team", eventId: 11, image: "https://images.unsplash.com/photo-1556910103-1c02745aae4d?auto=format&fit=crop&w=400&q=80" },
  { name: "Run Saigon", role: "Sports community", eventId: 4, image: "https://images.unsplash.com/photo-1552674605-db6ffd4facb5?auto=format&fit=crop&w=400&q=80" },
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

export default function Home() {
  const [query, setQuery] = useState("");
  const [activeCategory, setActiveCategory] = useState("Tất cả");
  const [liked, setLiked] = useState<number[]>([]);
  const [menuOpen, setMenuOpen] = useState(false);
  const [heroIndex, setHeroIndex] = useState(0);
  const [selectedEvent, setSelectedEvent] = useState<EventItem | null>(null);
  const [accountOpen, setAccountOpen] = useState(false);
  const [accountName, setAccountName] = useState<string | null>(null);
  const [subscribed, setSubscribed] = useState(false);
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
    <main>
      <header className="site-header">
        <div className="topbar shell">
          <a className="brand" href="#top" aria-label="EventGo - Trang chủ">
            <span className="brand-mark">E</span>
            <span>event<span>go</span></span>
          </a>
          <label className="header-search">
            <Icon name="search" />
            <input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Tìm sự kiện, nghệ sĩ, địa điểm..." />
            <kbd>⌘ K</kbd>
          </label>
          <nav className="desktop-actions" aria-label="Tài khoản">
            <a href="#organizer">Tạo sự kiện</a>
            <a href="#tickets"><Icon name="ticket" /> Vé của tôi</a>
          </nav>
          <button
            className="account-button"
            onClick={() => setAccountOpen(true)}
          >
            <Icon name="user" />
            {accountName ?? "Đăng nhập"}
          </button>
          <button className="menu-toggle" onClick={() => setMenuOpen(!menuOpen)} aria-label="Mở menu" aria-expanded={menuOpen}><span /><span /></button>
        </div>
        <nav className="category-nav" aria-label="Danh mục sự kiện">
          <div className="category-strip shell">
            {categories.map((category) => (
              <button key={category.label} className={activeCategory === category.label ? "active" : ""} onClick={() => selectCategory(category.label)}>
                <span>{category.icon}</span>{category.label}
              </button>
            ))}
          </div>
        </nav>
        <div className={`mobile-menu ${menuOpen ? "open" : ""}`}>
          <a href="#events">Khám phá sự kiện</a><a href="#organizer">Tạo sự kiện</a><a href="#tickets">Vé của tôi</a><button onClick={() => setAccountOpen(true)}>Đăng nhập</button>
        </div>
      </header>

      <section className="hero" id="top">
        <img key={hero.event.id} src={hero.event.image} alt={hero.event.title} />
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

      <section className="discovery-section">
        <div className="shell">
          <div className="mini-section-head">
            <div><span>★</span><h2>Gương mặt nổi bật</h2></div>
            <a href="#events">Xem tất cả <Icon name="arrow" /></a>
          </div>
          <div className="people-rail">
            {featuredPeople.map((person) => (
              <button key={person.name} onClick={() => setSelectedEvent(events.find((event) => event.id === person.eventId) ?? null)}>
                <span><img src={person.image} alt={person.name} /><i>✓</i></span>
                <strong>{person.name}</strong>
                <small>{person.role}</small>
              </button>
            ))}
          </div>

          <div className="mini-section-head event-row-title">
            <div><span>✦</span><h2>Sự kiện đặc biệt</h2></div>
            <a href="#events">Khám phá thêm <Icon name="arrow" /></a>
          </div>
          <div className="poster-rail">
            {[events[4], events[6], events[8], events[9], events[11]].map((event) => (
              <article key={event.id} onClick={() => setSelectedEvent(event)}>
                <div><img src={event.image} alt={event.title} /><span>{event.category}</span></div>
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
                <img src={event.image} alt={event.title} />
                <span><small>{event.category}</small><strong>{event.title}</strong><em>{event.price}</em></span>
              </button>
            ))}
          </div>
        </div>
      </section>

      <section className="section shell" id="events">
        <div className="section-head">
          <div><p className="kicker">ĐANG ĐƯỢC QUAN TÂM</p><h2>Sự kiện nổi bật</h2></div>
          <button className="text-link" onClick={() => { setActiveCategory("Tất cả"); setQuery(""); }}>Xem tất cả <Icon name="arrow" /></button>
        </div>
        <div className="event-grid">
          {visibleEvents.slice(0, 4).map((event) => (
            <article className="event-card" key={event.id}>
              <div className="event-image">
                <img src={event.image} alt={event.title} />
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

      <section className="spotlight-section">
        <div className="shell">
          <div className="section-head light-head">
            <div><p className="kicker">LỊCH HẸN SẮP TỚI</p><h2>Cuối tuần này</h2></div>
            <span className="week-label">28 — 30.08.2026</span>
          </div>
          <div className="spotlight-layout">
            <button className="spotlight-main" onClick={() => setSelectedEvent(events[0])}>
              <img src={events[0].image} alt={events[0].title} />
              <span className="spotlight-count">01</span>
              <div><p>{events[0].date}</p><h3>{events[0].title}</h3><span>{events[0].price} <Icon name="arrow" /></span></div>
            </button>
            <div className="agenda-list">
              {events.slice(1, 5).map((event, index) => (
                <button key={event.id} onClick={() => setSelectedEvent(event)}>
                  <time><b>{event.date.split(" · ")[1]?.split(".")[0]}</b><span>THÁNG {event.date.split(".")[1]}</span></time>
                  <div><small>{event.category}</small><h3>{event.title}</h3><p>{event.place}</p></div>
                  <span className="agenda-number">0{index + 2}</span>
                </button>
              ))}
            </div>
          </div>
        </div>
      </section>

      <section className="section shell recommendation-section">
        <div className="section-head">
          <div><p className="kicker">TÌM THẤY ĐIỀU BẠN THÍCH</p><h2>Dành riêng cho bạn</h2></div>
          <p className="section-note">Một tuyển chọn nhiều sắc màu — từ âm nhạc, sân khấu đến những trải nghiệm học hỏi mới.</p>
        </div>
        <div className="editorial-grid">
          {events.slice(1, 6).map((event, index) => (
            <article className={index === 0 ? "editorial-card wide" : "editorial-card"} key={event.id} onClick={() => setSelectedEvent(event)}>
              <div><img src={event.image} alt={event.title} /><button onClick={(click) => { click.stopPropagation(); toggleLike(event.id); }} className={liked.includes(event.id) ? "liked" : ""} aria-label="Lưu sự kiện"><Icon name="heart" /></button></div>
              <p>{event.category} · {event.date}</p>
              <h3>{event.title}</h3>
              <span>{event.price}</span>
            </article>
          ))}
        </div>
      </section>

      <section className="fresh-section shell">
        <div className="fresh-head">
          <div><p className="kicker">VỪA LÊN KỆ</p><h2>Mới mở bán</h2></div>
          <div className="fresh-tabs" role="tablist" aria-label="Khoảng thời gian">
            <button role="tab" aria-selected={salesTab === "week"} className={salesTab === "week" ? "active" : ""} onClick={() => setSalesTab("week")}>Cuối tuần này</button>
            <button role="tab" aria-selected={salesTab === "month"} className={salesTab === "month" ? "active" : ""} onClick={() => setSalesTab("month")}>Tháng này</button>
          </div>
        </div>
        <div className="fresh-grid">
          {saleEvents.map((event) => (
            <article key={event.id} onClick={() => setSelectedEvent(event)}>
              <div><img src={event.image} alt={event.title} /><span>{event.category}</span></div>
              <h3>{event.title}</h3>
              <p>{event.price}</p>
              <time>{event.date}</time>
            </article>
          ))}
        </div>
      </section>

      <section className="city-section">
        <div className="city-image"><img src="https://images.unsplash.com/photo-1575388798260-5342b39063cf?auto=format&fit=crop&w=1800&q=85" alt="Thành phố Hồ Chí Minh về đêm" /></div>
        <div className="city-copy">
          <p className="kicker">KHÁM PHÁ THÀNH PHỐ</p>
          <h2>Sài Gòn không ngủ.<br />Bạn cũng đừng vội.</h2>
          <p>Từ sân khấu nhỏ trong con hẻm đến đại nhạc hội bên sông — luôn có một trải nghiệm đang chờ bạn mở cửa.</p>
          <a className="button dark-button" href="#events">Xem sự kiện tại TP.HCM <Icon name="arrow" /></a>
        </div>
      </section>

      <section className="organizer-section" id="organizer">
        <div className="organizer-inner shell">
          <p className="kicker">DÀNH CHO NHÀ TỔ CHỨC</p>
          <h2>Một ý tưởng lớn<br />xứng đáng với sân khấu lớn.</h2>
          <p>Tạo trang sự kiện, quản lý hạng vé và tiếp cận đúng khán giả — tất cả trong một nơi.</p>
          <button className="button primary" onClick={() => setAccountOpen(true)}>Bắt đầu tạo sự kiện <Icon name="arrow" /></button>
          <div className="organizer-stats"><span><b>1.200+</b><small>Sự kiện mỗi năm</small></span><span><b>2,4 triệu</b><small>Người tham dự</small></span><span><b>98%</b><small>Vé điện tử</small></span></div>
        </div>
      </section>

      <section className="newsletter shell">
        <div><p className="kicker">ĐỪNG BỎ LỠ</p><h2>Nhận lịch vui<br />mỗi thứ Năm.</h2></div>
        <form onSubmit={(event) => { event.preventDefault(); setSubscribed(true); }}>
          {subscribed ? <p className="success-message">✓ Đã đăng ký! Hẹn gặp bạn trong bản tin tuần này.</p> : <><label htmlFor="email">Email của bạn</label><div><input id="email" type="email" required placeholder="ban@example.com" /><button aria-label="Đăng ký"><Icon name="arrow" /></button></div><small>Bằng việc đăng ký, bạn đồng ý nhận tin về sự kiện mới từ EventGo.</small></>}
        </form>
      </section>

      <footer className="footer" id="tickets">
        <div className="shell footer-grid">
          <div className="footer-brand"><a className="brand" href="#top"><span className="brand-mark">E</span><span>event<span>go</span></span></a><p>Chạm vé. Chạm khoảnh khắc.</p><div className="socials"><a href="#" aria-label="Facebook">f</a><a href="#" aria-label="Instagram">◎</a><a href="#" aria-label="TikTok">♪</a></div></div>
          <div><h3>Khám phá</h3><a href="#events">Sự kiện nổi bật</a><a href="#events">Âm nhạc</a><a href="#events">Sân khấu</a><a href="#events">Thể thao</a></div>
          <div><h3>Hỗ trợ</h3><a href="#">Trung tâm trợ giúp</a><a href="#">Quy chế hoạt động</a><a href="#">Chính sách bảo mật</a><a href="#">Liên hệ</a></div>
          <div><h3>Nhà tổ chức</h3><a href="#organizer">Tạo sự kiện</a><a href="#organizer">Giải pháp bán vé</a><a href="#organizer">Hợp tác truyền thông</a></div>
        </div>
        <div className="shell footer-bottom"><span>© 2026 EventGo. Frontend concept.</span><span>Việt Nam · VND · Tiếng Việt</span></div>
      </footer>

      {selectedEvent && (
        <div className="modal-backdrop" role="presentation" onMouseDown={() => setSelectedEvent(null)}>
          <section className="event-modal" role="dialog" aria-modal="true" aria-label="Chi tiết sự kiện" onMouseDown={(event) => event.stopPropagation()}>
            <button className="modal-close" onClick={() => setSelectedEvent(null)} aria-label="Đóng">×</button>
            <img src={selectedEvent.image} alt={selectedEvent.title} />
            <div><p className="kicker">{selectedEvent.category}</p><h2>{selectedEvent.title}</h2><p className="modal-date">{selectedEvent.date}</p><p className="modal-place"><Icon name="pin" />{selectedEvent.place}</p><div className="modal-buy"><strong>{selectedEvent.price}</strong><button className="button primary" onClick={() => setAccountOpen(true)}>Chọn vé <Icon name="arrow" /></button></div></div>
          </section>
        </div>
      )}

      <AccountPanel
        open={accountOpen}
        onClose={() => setAccountOpen(false)}
        onUserChange={(name) => {
          setAccountName(name);

          if (name) {
            setSelectedEvent(null);
            setMenuOpen(false);
          }
        }}
      />
    </main>
  );
}
