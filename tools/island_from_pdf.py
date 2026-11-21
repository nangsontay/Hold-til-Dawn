# Owner: Vu Duc Viet Anh. Builds maps/island_map.json (the island the game is played on) from the level-design
# PDF "Hold 'til Dawn - Island Map" (page 1). Not part of the game; the PDF is not in the repository.
#
#   python -I tools/island_from_pdf.py <Hold_til_Dawn_Island_Map.pdf> [maps/island_map.json] [--preview out.png]
#
# The PDF's map is one background picture (sea, beaches, grass, hills, trees) with vector drawings and text on
# top (routes, trails, landmark numbers, boss spawns, enemy bands). The picture is read pixel by pixel, the
# drawings and text by their coordinates. Everything is written in tiles from the outpost centre, +x east,
# +y south (the game's screen axes): the PDF's own scale (its spawn bands are at 32.5..42.5 tiles, its boss
# spawns at 67.5) puts 4.357 pt and about 10 picture pixels in one tile.
#
# Output (all in tiles):
#   coast      land outline (where the sea starts)            grass   inner edge of the beach
#   shallows   outlines 2, 5 and 9 tiles out to sea            hills   per hill, rings from the foot to the top
#   shade      darker woodland floor                           trees   one point per tree in the picture
#   routes     the three main gathering routes                 trails  exploration trails
#   landmarks  id, name, what is there, position               bays    sea names and where to write them
import json
import sys

import cv2
import fitz
import numpy as np

CX, CY, PT_PER_TILE = 473.0, 510.0, 4.357

LANDMARKS = [
    (1, "Lighthouse Outpost", "Spawn, crafting", "base"),
    (2, "Coconut Grove", "Wood", "wood"),
    (3, "Limestone Flats", "Stone", "stone"),
    (4, "Herb Meadow", "Herbs", "herb"),
    (5, "Bayside Wreck", "Titanium", "titanium"),
    (6, "Ruined Base", "Titanium", "titanium"),
    (7, "Stranded Convoy", "Titanium", "titanium"),
    (8, "Mangrove Swamp", "Daytime roamers", "swamp"),
    (9, "Weather Station", "NW landmark", "landmark"),
    (10, "Old Fishing Pier", "West coast landmark", "landmark"),
    (11, "Northwind Slope", "Uphill trail", "landmark"),
    (12, "Coral Lookout", "NE landmark", "landmark"),
    (13, "Container Yard", "Titanium", "titanium"),
    (14, "Beached Shipwreck", "Titanium", "titanium"),
    (15, "Rainwater Pond", "Herbs", "herb"),
    (16, "Southwest Cave", "Stone", "stone"),
    (17, "South Cape Lookout", "South cape landmark", "landmark"),
    (18, "Abandoned Herb Garden", "Herbs", "herb"),
]
BAYS = {("CRESCENT", "BAY"): "Crescent Bay", ("CORAL", "BAY"): "Coral Bay", ("MANGROVE", "INLET"): "Mangrove Inlet", ("SOUTH", "COVE"): "South Cove"}


def to_tiles(x, y):
    return [round((x - CX) / PT_PER_TILE, 2), round((y - CY) / PT_PER_TILE, 2)]


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    preview = None
    if "--preview" in sys.argv:
        preview = sys.argv[sys.argv.index("--preview") + 1]
        args.remove(preview)
    pdf = args[0]
    out = args[1] if len(args) > 1 else "maps/island_map.json"
    doc = fitz.open(pdf)
    page = doc[0]

    # --- the picture -------------------------------------------------------------------------------------
    info = [i for i in page.get_image_info(xrefs=True) if i["xref"]][0]
    raw = doc.extract_image(info["xref"])
    img = cv2.imdecode(np.frombuffer(raw["image"], np.uint8), cv2.IMREAD_COLOR)[:, :, ::-1].astype(np.int32)
    x0, y0, x1, y1 = info["bbox"]
    px_per_pt = img.shape[1] / (x1 - x0)
    tile_px = PT_PER_TILE * px_per_pt

    def px_to_tiles(p):
        return to_tiles(x0 + p[0] / px_per_pt, y0 + p[1] / px_per_pt)

    R, G, B = img[:, :, 0], img[:, :, 1], img[:, :, 2]
    k = lambda tiles: max(1, int(round(tiles * tile_px)))

    def largest(mask):
        n, lab, stats, _ = cv2.connectedComponentsWithStats(mask.astype(np.uint8), 8)
        if n <= 1:
            return mask
        best = 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))
        return lab == best

    def fill_holes(mask):
        m = mask.astype(np.uint8)
        cs, _ = cv2.findContours(m, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
        out = np.zeros_like(m)
        cv2.drawContours(out, cs, -1, 1, -1)
        return out.astype(bool)

    def smooth(mask, tiles):
        m = mask.astype(np.uint8) * 255
        m = cv2.GaussianBlur(m, (0, 0), tiles * tile_px)
        return m > 127

    def outlines(mask, eps_tiles=0.25, min_area_tiles=4.0):
        cs, _ = cv2.findContours(mask.astype(np.uint8), cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
        polys = []
        for c in cs:
            if cv2.contourArea(c) < min_area_tiles * tile_px * tile_px:
                continue
            c = cv2.approxPolyDP(c, eps_tiles * tile_px, True)
            polys.append([px_to_tiles((p[0][0], p[0][1])) for p in c])
        return polys

    land = fill_holes(largest((G - B) >= 10))
    land = smooth(land, 0.6)
    sand = (R - G > 12) & (R > 140)
    grass = land & ~cv2.dilate(sand.astype(np.uint8), np.ones((3, 3), np.uint8)).astype(bool)
    grass = fill_holes(largest(smooth(grass, 0.8)))
    # the sea floor drops away in steps
    sea_d = cv2.distanceTransform((~land).astype(np.uint8), cv2.DIST_L2, 5) / tile_px
    shallows = [outlines(fill_holes(largest(sea_d < d)), 0.4)[0] for d in (2.0, 5.0, 9.0)]
    # hills: the grey-brown patches with contour rings round them
    hill = grass & (np.abs(R - G) < 14) & (G - B > 10) & (R > 110)
    # not the pale rim of the beach, nor the three dirt roads along the door axes
    edge = cv2.distanceTransform(grass.astype(np.uint8), cv2.DIST_L2, 5) / tile_px
    ty, tx = np.mgrid[0:img.shape[0], 0:img.shape[1]]
    tx = (x0 + tx / px_per_pt - CX) / PT_PER_TILE
    ty = (y0 + ty / px_per_pt - CY) / PT_PER_TILE
    road = ((np.abs(tx) < 2.5) & (ty < 0)) | (np.abs(ty) < 2.5)
    hill = hill & (edge > 2.0) & ~road
    hill = cv2.morphologyEx(hill.astype(np.uint8), cv2.MORPH_OPEN, np.ones((5, 5), np.uint8))
    hill = smooth(cv2.morphologyEx(hill, cv2.MORPH_CLOSE, np.ones((k(1.5), k(1.5)), np.uint8)).astype(bool), 0.5)
    n, lab, stats, _ = cv2.connectedComponentsWithStats(hill.astype(np.uint8), 8)
    hills = []
    for i in range(1, n):
        if stats[i, cv2.CC_STAT_AREA] < 6 * tile_px * tile_px:
            continue
        core = lab == i
        inside = cv2.distanceTransform(core.astype(np.uint8), cv2.DIST_L2, 5) / tile_px
        outside = cv2.distanceTransform((~core).astype(np.uint8), cv2.DIST_L2, 5) / tile_px
        rings = []
        for d in (4.5, 3.0, 1.5, 0.0, -1.5, -3.0):
            m = (outside < d) if d > 0 else (inside > -d) if d < 0 else core
            m = m & grass
            ps = outlines(m, 0.3, 1.0)
            if ps:
                rings.append(max(ps, key=len))
        ys, xs = np.nonzero(core)
        hills.append({"top": px_to_tiles((xs.mean(), ys.mean())), "rings": rings})
    # woodland: darker floor, and a tree for each dark dot
    dark = grass & ((R + G + B) < 300) & (G - B > 12)
    shade = outlines(smooth(dark, 1.6) & grass, 0.4, 12.0)
    dots = grass & ((R + G + B) < 272) & (G - B > 14)
    n, lab, stats, cents = cv2.connectedComponentsWithStats(dots.astype(np.uint8), 8)
    trees = [px_to_tiles(cents[i]) for i in range(1, n) if 6 <= stats[i, cv2.CC_STAT_AREA] <= 220]

    # --- the drawings and text ---------------------------------------------------------------------------
    routes, trails = [], []
    for g in page.get_drawings():
        col = tuple(round(x, 2) for x in (g.get("color") or ()))
        w = round(g.get("width") or 0, 2)
        if col == (1.0, 0.85, 0.55) and w == 1.55:
            into = routes
        elif col == (0.9, 0.79, 0.59) and w == 1.05:
            into = trails
        else:
            continue
        r = g["rect"]
        if r.x0 > x1 or r.y0 > y1:
            continue  # the legend
        pts = []
        for it in g["items"]:
            if it[0] == "l":
                if not pts:
                    pts.append(to_tiles(it[1].x, it[1].y))
                pts.append(to_tiles(it[2].x, it[2].y))
            elif it[0] == "c":
                if not pts:
                    pts.append(to_tiles(it[1].x, it[1].y))
                for q in (it[2], it[3], it[4]):
                    pts.append(to_tiles(q.x, q.y))
        into.append(pts)
    words = [w for w in page.get_text("words") if x0 < w[0] < x1 and y0 < w[1] < y1]
    at = {}
    for w in words:
        # (the scale bar's numbers sit in the corner, off the island)
        if w[4].isdigit() and len(w[4]) == 2 and abs(to_tiles(w[0], w[1])[0]) < 78 and to_tiles(w[0], w[1])[1] < 66:
            at[int(w[4])] = to_tiles((w[0] + w[2]) / 2, (w[1] + w[3]) / 2)
    landmarks = [{"id": i, "name": n, "what": what, "kind": kind, "pos": at[i]} for i, n, what, kind in LANDMARKS]
    bays = []
    for a, b in zip(words, words[1:]):
        if (a[4], b[4]) in BAYS:
            bays.append({"name": BAYS[(a[4], b[4])], "pos": to_tiles((a[0] + b[2]) / 2, (a[1] + b[3]) / 2)})

    data = {
        "source": "Hold 'til Dawn - Island Map (level design PDF, page 1), via tools/island_from_pdf.py",
        "units": "tiles from the outpost centre, +x east, +y south",
        "coast": outlines(land, 0.2)[0],
        "grass": outlines(grass, 0.2)[0],
        "shallows": shallows,
        "hills": hills,
        "shade": shade,
        "trees": trees,
        "routes": routes,
        "trails": trails,
        "landmarks": landmarks,
        "bays": bays,
    }
    with open(out, "w", encoding="utf-8") as f:
        json.dump(data, f, separators=(",", ":"))
    print(f"{out}: coast {len(data['coast'])} pts, grass {len(data['grass'])}, hills {len(hills)}, shade {len(shade)}, "
          f"trees {len(trees)}, routes {len(routes)}, trails {len(trails)}, landmarks {len(landmarks)}, bays {len(bays)}")

    if preview:
        S, O = 5, 90
        can = np.full(((O * 2) * S, (O * 2) * S, 3), (46, 82, 102), np.uint8)

        def P(poly):
            return np.array([[int((x + O) * S), int((y + O) * S)] for x, y in poly], np.int32)

        for i, sh in enumerate(shallows[::-1]):
            cv2.fillPoly(can, [P(sh)], (60 + 20 * i, 110 + 15 * i, 130 + 10 * i))
        cv2.fillPoly(can, [P(data["coast"])], (201, 180, 138))
        cv2.fillPoly(can, [P(data["grass"])], (100, 128, 64))
        for s in shade:
            cv2.fillPoly(can, [P(s)], (80, 108, 56))
        for h in hills:
            for j, r in enumerate(h["rings"]):
                cv2.fillPoly(can, [P(r)], (120 + 12 * j, 128 + 8 * j, 90 + 10 * j))
                cv2.polylines(can, [P(r)], True, (200, 200, 170), 1)
        for t in trees:
            cv2.circle(can, tuple(P([t])[0]), 3, (40, 70, 50), -1)
        for r in routes:
            cv2.polylines(can, [P(r)], False, (250, 210, 140), 2)
        for r in trails:
            cv2.polylines(can, [P(r)], False, (230, 200, 150), 1)
        for l in landmarks:
            c = tuple(P([l["pos"]])[0])
            cv2.circle(can, c, 8, (20, 40, 50), -1)
            cv2.putText(can, f"{l['id']:02d}", (c[0] - 7, c[1] + 4), cv2.FONT_HERSHEY_SIMPLEX, 0.35, (255, 220, 150), 1)
        cv2.rectangle(can, tuple(P([[-5.5, -3.5]])[0]), tuple(P([[5.5, 3.5]])[0]), (80, 80, 90), -1)
        cv2.imwrite(preview, can[:, :, ::-1])


if __name__ == "__main__":
    main()
