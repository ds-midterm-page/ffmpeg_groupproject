"""Matching-frame loop search and seam blend ported from the supplied make_loop.py."""
import numpy as np

FPS = 30
WIN = 2
ANALYSIS_SIDE = 64
MAX_FRAMES = 1500
TARGET_DUR = 2.95
SCALE = "scale='if(gt(iw,ih),512,-2)':'if(gt(iw,ih),-2,512)':flags=lanczos"

def distance_matrix(X):
    X = X.astype(np.float64)
    X -= X.mean(axis=0)
    sq = (X * X).sum(axis=1)
    d2 = sq[:, None] + sq[None, :] - 2.0 * (X @ X.T)
    return np.sqrt(np.maximum(d2, 0.0) / X.shape[1])  # RMS-разница, шкала 0..255


def find_loop(D, min_len, max_len):
    """Возвращает (s, e, cost): петля = кадры s..e-1, шов e-1 -> s."""
    N = D.shape[0]
    C = np.zeros((N, N))
    cnt = np.zeros((N, N))
    for k in range(-WIN, WIN + 1):
        if k >= 0:
            C[:N - k, :N - k] += D[k:, k:]
            cnt[:N - k, :N - k] += 1
        else:
            m = -k
            C[m:, m:] += D[:N - m, :N - m]
            cnt[m:, m:] += 1
    C = C / np.maximum(cnt, 1)

    mot = np.array([D[i, i + 1] for i in range(N - 1)])
    pref = np.concatenate([[0.0], np.cumsum(mot)])
    mean_mot = mot.mean() if len(mot) else 0.0

    max_len = min(max_len, N - 1 - 2 * WIN)
    if max_len < min_len:
        return None

    best = None
    for L in range(min_len, max_len + 1):
        s = np.arange(WIN, N - WIN - L)
        if len(s) == 0:
            continue
        e = s + L
        cost = C[s, e]
        # петля не должна быть "стоячей" паузой
        loop_mot = (pref[e - 1] - pref[s]) / max(L - 1, 1)
        ok = loop_mot >= 0.4 * mean_mot
        if not ok.any():
            continue
        score = cost * (1.0 + 0.4 * (1.0 - L / max_len))  # лёгкое предпочтение длинным петлям
        score = np.where(ok, score, np.inf)
        i = int(np.argmin(score))
        if np.isfinite(score[i]) and (best is None or score[i] < best[0]):
            best = (float(score[i]), int(s[i]), int(e[i]), float(cost[i]))
    if best is None:
        return None
    return best[1], best[2], best[3]


def build_filter(s, e, k, speed, out_fps):
    head = f"[0:v]fps={FPS},{SCALE},format=gbrap"
    tail = ""
    if speed > 1.001:
        tail += f",setpts=PTS/{speed:.5f}"
    tail += f",fps={out_fps}"
    if k > 0:
        return (
            f"{head},split=3[a][b][c];"
            f"[a]trim=start_frame={s}:end_frame={s + k},setpts=PTS-STARTPTS[ha];"
            f"[b]trim=start_frame={e}:end_frame={e + k},setpts=PTS-STARTPTS[hb];"
            f"[c]trim=start_frame={s + k}:end_frame={e},setpts=PTS-STARTPTS[rest];"
            f"[ha][hb]blend=all_expr='A*(N+1)/({k}+1)+B*(1-(N+1)/({k}+1))'[hd];"
            f"[hd][rest]concat=n=2:v=1:a=0{tail}[v]"
        )
    return f"{head},trim=start_frame={s}:end_frame={e},setpts=PTS-STARTPTS{tail}[v]"


