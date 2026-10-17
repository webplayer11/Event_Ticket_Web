import { useState, useMemo, useEffect, useCallback } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import AccountPanel from "../../components/AccountPanel";
import { getPublishedEvents, getEventCategories } from "../../services/eventService";
import type { EventResponse, EventCategoryResponse } from "../../types";

// -------- Helpers --------
function formatEventDate(startsAt: string): string {
  const d = new Date(startsAt);
  const hours = d.getHours().toString().padStart(2, "0");
  const minutes = d.getMinutes().toString().padStart(2, "0");
  const day = d.getDate().toString().padStart(2, "0");
  const month = (d.getMonth() + 1).toString().padStart(2, "0");
  const year = d.getFullYear();
  return `${hours}:${minutes} · ${day}.${month}.${year}`;
}

function formatShortDate(startsAt: string): string {
  const d = new Date(startsAt);
  return `${d.getDate().toString().padStart(2, "0")}.${(d.getMonth() + 1).toString().padStart(2, "0")}`;
}

function formatPrice(ticketTypes: EventResponse["ticketTypes"]): string {
  if (!ticketTypes || ticketTypes.length === 0) return "Liên hệ BTC";
  const prices = ticketTypes.map((t) => t.price).filter((p) => p >= 0);
  if (prices.length === 0) return "Liên hệ BTC";
  const min = Math.min(...prices);
  if (min === 0) return "Miễn phí";
  return `Từ ${min.toLocaleString("vi-VN")}đ`;
}

// -------- Static category config --------
const CATEGORY_ICONS: Record<string, string> = {
  "am-nhac": "♫",
  "san-khau": "✦",
  "the-thao": "⚽",
  "hoi-thao": "◫",
  "dien-anh": "▶",
  workshop: "✎",
};

const CATEGORY_META: Record<
  string,
  { kicker: string; description: string }
> = {
  "am-nhac": {
    kicker: "CONCERT & LIVE MUSIC",
    description:
      "Đắm chìm trong không gian âm thanh bùng nổ, ánh sáng mãn nhãn cùng những nghệ sĩ hàng đầu.",
  },
  "san-khau": {
    kicker: "THEATRE & PERFORMING ARTS",
    description:
      "Những tác phẩm kịch đương đại, nghệ thuật biểu diễn sâu lắng và lay động lòng người.",
  },
  "the-thao": {
    kicker: "SPORTS & OUTDOOR",
    description:
      "Khơi dậy tinh thần bứt phá với các giải chạy đêm, giải đấu phong trào và sự kiện thể thao cộng đồng.",
  },
  "hoi-thao": {
    kicker: "CONFERENCES & FORUMS",
    description:
      "Cập nhật kiến thức chuyên sâu, xu hướng đổi mới sáng tạo và cơ hội kết nối với các chuyên gia đầu ngành.",
  },
  "dien-anh": {
    kicker: "CINEMA & EXHIBITIONS",
    description:
      "Thưởng thức các tuần lễ phim độc lập, triển lãm nghệ thuật thị giác và không gian tương tác đa chiều.",
  },
  workshop: {
    kicker: "CREATIVE WORKSHOPS",
    description:
      "Thực hành kỹ năng thực tế, trải nghiệm văn hóa ẩm thực, thủ công sáng tạo và lối sống lành mạnh.",
  },
};

// -------- Sub-components --------
function Icon({
  name,
}: {
  name: "search" | "ticket" | "user" | "heart" | "arrow" | "pin";
}) {
  const paths = {
    search: (
      <>
        <circle cx="11" cy="11" r="7" />
        <path d="m20 20-4-4" />
      </>
    ),
    ticket: (
      <>
        <path d="M4 5h16v4a3 3 0 0 0 0 6v4H4v-4a3 3 0 0 0 0-6V5Z" />
        <path d="M12 7v10" />
      </>
    ),
    user: (
      <>
        <circle cx="12" cy="8" r="4" />
        <path d="M4.5 21a7.5 7.5 0 0 1 15 0" />
      </>
    ),
    heart: (
      <path d="M20.8 4.6a5.5 5.5 0 0 0-7.8 0L12 5.7l-1.1-1.1a5.5 5.5 0 0 0-7.8 7.8l1.1 1.1L12 21l7.8-7.5 1.1-1.1a5.5 5.5 0 0 0-.1-7.8Z" />
    ),
    arrow: (
      <>
        <path d="M5 12h14" />
        <path d="m13 6 6 6-6 6" />
      </>
    ),
    pin: (
      <>
        <path d="M20 10c0 5-8 11-8 11S4 15 4 10a8 8 0 1 1 16 0Z" />
        <circle cx="12" cy="10" r="2" />
      </>
    ),
  };
  return (
    <svg
      aria-hidden="true"
      className="icon"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      {paths[name]}
    </svg>
  );
}

function RenderEventImage({
  image,
  title,
  category,
}: {
  image?: string;
  title: string;
  category?: string;
}) {
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

// -------- Main Component --------
export const HomePage = () => {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const urlQuery = searchParams.get("q") || "";
  const [query, setQuery] = useState(urlQuery);

  // API data
  const [events, setEvents] = useState<EventResponse[]>([]);
  const [categories, setCategories] = useState<EventCategoryResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // UI state
  const [activeCategory, setActiveCategory] = useState("Tất cả");
  const [likedIds, setLikedIds] = useState<string[]>([]);
  const [heroIndex, setHeroIndex] = useState(0);
  const [selectedEvent, setSelectedEvent] = useState<EventResponse | null>(
    null,
  );
  const [accountOpen, setAccountOpen] = useState(false);

  useEffect(() => {
    setQuery(urlQuery);
  }, [urlQuery]);

  // Fetch data from API
  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    Promise.all([getPublishedEvents(), getEventCategories()])
      .then(([evts, cats]) => {
        if (cancelled) return;
        setEvents(evts);
        setCategories(cats);
        setError(null);
      })
      .catch((err) => {
        if (cancelled) return;
        setError(err?.message ?? "Không thể tải dữ liệu.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  // Build nav categories (API + "Tất cả" prepended)
  const navCategories = useMemo(() => {
    const base = [{ label: "Tất cả", slug: "all", icon: "❖" }];
    const apiCats = categories.map((c) => ({
      label: c.name,
      slug: c.slug,
      icon: CATEGORY_ICONS[c.slug] ?? "✦",
    }));
    return [...base, ...apiCats];
  }, [categories]);

  // Category sections with events
  const categorySections = useMemo(() => {
    return categories.map((cat) => {
      const meta = CATEGORY_META[cat.slug] ?? {
        kicker: cat.name.toUpperCase(),
        description: "",
      };
      const catEvents = events.filter((e) => e.categoryId === cat.id);
      return { ...cat, ...meta, events: catEvents };
    });
  }, [categories, events]);

  const visibleEvents = useMemo(() => {
    return events.filter((event) => {
      const categoryMatch =
        activeCategory === "Tất cả" || event.categoryName === activeCategory;
      const searchMatch = `${event.title} ${event.venueName} ${event.city}`
        .toLowerCase()
        .includes(query.toLowerCase());
      return categoryMatch && searchMatch;
    });
  }, [query, activeCategory, events]);

  // Hero slides: first 3 events, or empty
  const heroSlides = useMemo(() => {
    const source = events.slice(0, 3);
    return source.map((ev, i) => ({
      event: ev,
      kicker:
        i === 0
          ? "Lựa chọn của EventGo"
          : i === 1
            ? "Sự kiện nổi bật"
            : "Đang được quan tâm",
      headline: ev.title,
      note: ev.description.slice(0, 100) + (ev.description.length > 100 ? "..." : ""),
      shortDate: formatShortDate(ev.startsAt),
    }));
  }, [events]);

  const hero = heroSlides[heroIndex] ?? null;

  const toggleLike = useCallback(
    (id: string) =>
      setLikedIds((current) =>
        current.includes(id)
          ? current.filter((item) => item !== id)
          : [...current, id],
      ),
    [],
  );

  const scrollToCategory = (slug: string, label: string) => {
    setActiveCategory(label);
    const targetId = slug === "all" ? "events" : `category-${slug}`;
    const el = document.getElementById(targetId);
    if (el) {
      const yOffset = -120;
      const y = el.getBoundingClientRect().top + window.pageYOffset + yOffset;
      window.scrollTo({ top: y, behavior: "smooth" });
    }
  };

  useEffect(() => {
    const handleHash = () => {
      const hash = window.location.hash;
      if (hash && hash.startsWith("#category-")) {
        const targetId = hash.substring(1);
        const el = document.getElementById(targetId);
        if (el) {
          setTimeout(() => {
            const yOffset = -120;
            const y =
              el.getBoundingClientRect().top + window.pageYOffset + yOffset;
            window.scrollTo({ top: y, behavior: "smooth" });
          }, 150);
        }
      }
    };
    handleHash();
    window.addEventListener("hashchange", handleHash);
    return () => window.removeEventListener("hashchange", handleHash);
  }, []);

  // ---- Render ----
  return (
    <div className="home-container">
      {/* Category Strip */}
      <nav
        className="category-nav sticky top-16 z-20 bg-[#0d1117]/95 backdrop-blur border-b border-slate-800"
        aria-label="Danh mục sự kiện"
      >
        <div className="category-strip shell">
          {navCategories.map((category) => (
            <button
              key={category.label}
              className={activeCategory === category.label ? "active" : ""}
              onClick={() => scrollToCategory(category.slug, category.label)}
            >
              <span>{category.icon}</span>
              {category.label}
            </button>
          ))}
        </div>
      </nav>

      {/* Hero Section */}
      {hero ? (
        <section className="hero" id="top">
          <div className="hero-fallback-bg" />
          <div className="hero-shade" />
          <div className="hero-content shell">
            <div className="eyebrow">
              <span /> {hero.kicker}
            </div>
            <h1>{hero.headline}</h1>
            <p>{hero.note}</p>
            <div className="hero-actions">
              <a className="button primary" href="#events">
                Khám phá ngay <Icon name="arrow" />
              </a>
              <span>
                <b>{hero.shortDate}</b>
                <small>{hero.event.venueName}, {hero.event.city}</small>
              </span>
            </div>
            {heroSlides.length > 1 && (
              <div className="hero-dots" aria-label="Chọn banner">
                {heroSlides.map((slide, index) => (
                  <button
                    key={slide.event.id}
                    aria-label={`Banner ${index + 1}`}
                    className={heroIndex === index ? "active" : ""}
                    onClick={() => setHeroIndex(index)}
                  />
                ))}
              </div>
            )}
          </div>
          {heroSlides.length > 1 && (
            <span className="hero-index">
              0{heroIndex + 1} / 0{heroSlides.length}
            </span>
          )}
        </section>
      ) : (
        /* Fallback hero khi không có sự kiện */
        <section className="hero" id="top">
          <div className="hero-fallback-bg" />
          <div className="hero-shade" />
          <div className="hero-content shell">
            <div className="eyebrow">
              <span /> Chào mừng đến với EventGo
            </div>
            <h1>Sự kiện tuyệt vời đang chờ bạn.</h1>
            <p>
              Nền tảng mua bán vé sự kiện hàng đầu Việt Nam — Âm nhạc, Thể
              thao, Hội thảo và hơn thế nữa.
            </p>
            <div className="hero-actions">
              <a className="button primary" href="#events">
                Khám phá ngay <Icon name="arrow" />
              </a>
            </div>
          </div>
        </section>
      )}

      {/* Loading / Error */}
      {loading && (
        <div className="section shell" style={{ textAlign: "center", padding: "3rem 0" }}>
          <p style={{ color: "#94a3b8" }}>Đang tải sự kiện…</p>
        </div>
      )}
      {error && !loading && (
        <div className="section shell" style={{ textAlign: "center", padding: "3rem 0" }}>
          <p style={{ color: "#f87171" }}>{error}</p>
        </div>
      )}

      {!loading && !error && (
        <>
          {/* Discovery Section */}
          {events.length > 0 && (
            <section className="discovery-section">
              <div className="shell">
                <div className="mini-section-head event-row-title">
                  <div>
                    <span>✦</span>
                    <h2>Sự kiện đặc biệt</h2>
                  </div>
                  <a href="#events">
                    Khám phá thêm <Icon name="arrow" />
                  </a>
                </div>
                <div className="poster-rail">
                  {events.slice(0, 5).map((event) => (
                    <article key={event.id} onClick={() => setSelectedEvent(event)}>
                      <div>
                        <RenderEventImage
                          title={event.title}
                          category={event.categoryName}
                        />
                        <span>{event.categoryName}</span>
                      </div>
                      <p>{formatEventDate(event.startsAt)}</p>
                      <h3>{event.title}</h3>
                    </article>
                  ))}
                </div>

                {events.length >= 4 && (
                  <>
                    <div className="mini-section-head event-row-title">
                      <div>
                        <span>●</span>
                        <h2>Đang thịnh hành</h2>
                      </div>
                      <span className="live-badge">CẬP NHẬT TRỰC TIẾP</span>
                    </div>
                    <div className="trending-grid">
                      {events.slice(0, 4).map((event, index) => (
                        <button
                          key={event.id}
                          onClick={() => setSelectedEvent(event)}
                        >
                          <b>0{index + 1}</b>
                          <RenderEventImage
                            title={event.title}
                            category={event.categoryName}
                          />
                          <span>
                            <small>{event.categoryName}</small>
                            <strong>{event.title}</strong>
                            <em>{formatPrice(event.ticketTypes)}</em>
                          </span>
                        </button>
                      ))}
                    </div>
                  </>
                )}
              </div>
            </section>
          )}

          {/* All Events Grid */}
          <section className="section shell" id="events">
            <div className="section-head">
              <div>
                <p className="kicker">ĐANG ĐƯỢC QUAN TÂM</p>
                <h2>Sự kiện nổi bật</h2>
              </div>
              <button
                className="text-link"
                onClick={() => {
                  setActiveCategory("Tất cả");
                  setQuery("");
                }}
              >
                Xem tất cả <Icon name="arrow" />
              </button>
            </div>
            <div className="event-grid">
              {visibleEvents.slice(0, 4).map((event) => (
                <article className="event-card" key={event.id}>
                  <div className="event-image">
                    <RenderEventImage
                      title={event.title}
                      category={event.categoryName}
                    />
                    <span className="event-tag">{event.categoryName}</span>
                    <button
                      onClick={() => toggleLike(event.id)}
                      className={likedIds.includes(event.id) ? "liked" : ""}
                      aria-label="Lưu sự kiện"
                    >
                      <Icon name="heart" />
                    </button>
                  </div>
                  <div className="event-copy">
                    <p className="date">{formatEventDate(event.startsAt)}</p>
                    <h3>{event.title}</h3>
                    <p className="place">
                      <Icon name="pin" />
                      {event.venueName}, {event.city}
                    </p>
                    <div>
                      <strong>{formatPrice(event.ticketTypes)}</strong>
                      <button
                        className="card-arrow"
                        onClick={() => setSelectedEvent(event)}
                        aria-label={`Xem ${event.title}`}
                      >
                        <Icon name="arrow" />
                      </button>
                    </div>
                  </div>
                </article>
              ))}
            </div>
            {visibleEvents.length === 0 && (
              <div className="empty-state">
                <span>⌕</span>
                <h3>Chưa tìm thấy sự kiện</h3>
                <p>Thử từ khóa khác hoặc chọn lại danh mục.</p>
              </div>
            )}
          </section>

          {/* Category Sections */}
          <div className="category-sections space-y-16 py-8">
            {categorySections.map((cat) => {
              if (cat.events.length === 0) return null;
              return (
                <section
                  key={cat.slug}
                  id={`category-${cat.slug}`}
                  className="section shell scroll-mt-28"
                >
                  <div className="section-head mb-6">
                    <div>
                      <h2 className="text-2xl font-black text-white !text-white tracking-tight">
                        {cat.name}
                      </h2>
                      <p className="text-xs text-slate-400 mt-1 max-w-xl">
                        {cat.description}
                      </p>
                    </div>
                    <div className="flex items-center gap-2">
                      <span className="text-xs px-3 py-1 rounded-full bg-slate-800 text-slate-200 border border-slate-700/80 font-semibold">
                        {cat.events.length} sự kiện
                      </span>
                      <button
                        className="text-link"
                        onClick={() => {
                          if (cat.events.length > 0) {
                            setSelectedEvent(cat.events[0]);
                          }
                        }}
                      >
                        Xem nổi bật <Icon name="arrow" />
                      </button>
                    </div>
                  </div>

                  <div className="event-grid">
                    {cat.events.map((event) => (
                      <article className="event-card" key={event.id}>
                        <div className="event-image">
                          <RenderEventImage
                            title={event.title}
                            category={event.categoryName}
                          />
                          <span className="event-tag">{event.categoryName}</span>
                          <button
                            onClick={() => toggleLike(event.id)}
                            className={likedIds.includes(event.id) ? "liked" : ""}
                            aria-label="Lưu sự kiện"
                          >
                            <Icon name="heart" />
                          </button>
                        </div>
                        <div className="event-copy">
                          <p className="date">{formatEventDate(event.startsAt)}</p>
                          <h3>{event.title}</h3>
                          <p className="place">
                            <Icon name="pin" />
                            {event.venueName}, {event.city}
                          </p>
                          <div>
                            <strong>{formatPrice(event.ticketTypes)}</strong>
                            <button
                              className="card-arrow"
                              onClick={() => setSelectedEvent(event)}
                              aria-label={`Xem ${event.title}`}
                            >
                              <Icon name="arrow" />
                            </button>
                          </div>
                        </div>
                      </article>
                    ))}
                  </div>
                </section>
              );
            })}
          </div>
        </>
      )}

      {/* Selected Event Modal */}
      {selectedEvent && (
        <div
          className="modal-backdrop"
          role="presentation"
          onMouseDown={() => setSelectedEvent(null)}
        >
          <section
            className="event-modal"
            role="dialog"
            aria-modal="true"
            aria-label="Chi tiết sự kiện"
            onMouseDown={(event) => event.stopPropagation()}
          >
            <button
              className="modal-close"
              onClick={() => setSelectedEvent(null)}
              aria-label="Đóng"
            >
              ×
            </button>
            <div className="modal-image-wrapper">
              <RenderEventImage
                title={selectedEvent.title}
                category={selectedEvent.categoryName}
              />
            </div>
            <div>
              <p className="kicker">{selectedEvent.categoryName}</p>
              <h2>{selectedEvent.title}</h2>
              <p className="modal-date">{formatEventDate(selectedEvent.startsAt)}</p>
              <p className="modal-place">
                <Icon name="pin" />
                {selectedEvent.venueName}, {selectedEvent.city}
              </p>
              <div className="modal-buy">
                <strong>{formatPrice(selectedEvent.ticketTypes)}</strong>
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
