#!/usr/bin/env python3

import argparse
import math
import shutil
import subprocess
from pathlib import Path


PI = math.pi
EXTERNAL_SCALE = 25.0
LIGHT_MIX = 0.2
GUI_LIGHT_LEVEL = 1.2


def fract(value):
    return value - math.floor(value)


def clamp(value, minimum=0.0, maximum=1.0):
    return max(minimum, min(maximum, value))


def smoothstep(edge0, edge1, value):
    t = clamp((value - edge0) / (edge1 - edge0))
    return t * t * (3.0 - 2.0 * t)


def normalize(vector):
    length = math.sqrt(sum(component * component for component in vector))
    return tuple(component / length for component in vector)


def dot(left, right):
    return sum(a * b for a, b in zip(left, right))


def rand2d(x, y):
    value = (x * 12.9898 + y * 78.233) % 3.14
    return fract(math.sin(value) * 43758.5453)


def sword_frame(tick):
    value = tick % 30
    if value < 3:
        return 0
    if value < 6:
        return 1
    if value < 9:
        return 2
    if value < 11:
        return 3
    if value < 13:
        return 4
    if value < 14:
        return 5
    if value < 15:
        return 6
    if value < 16:
        return 7
    if value < 17:
        return 8
    if value < 18:
        return 7
    if value < 19:
        return 6
    if value < 20:
        return 5
    if value < 22:
        return 4
    if value < 24:
        return 3
    if value < 27:
        return 2
    return 1


def cosmic_frame_0(tick):
    value = tick % 10
    return 0 if value < 7 else value - 6


def cosmic_frame_1(tick):
    value = tick % 23
    if value < 4:
        return 0
    if value < 5:
        return 1
    if value < 14:
        return 0
    if value < 15:
        return 2
    if value < 22:
        return 0
    return 3


def cosmic_frame_2(tick):
    value = tick % 31
    if value < 16:
        return 0
    if value < 19:
        return 1
    if value < 21:
        return 2
    if value < 22:
        return 3
    if value < 23:
        return 4
    if value < 24:
        return 3
    if value < 25:
        return 4
    if value < 26:
        return 3
    if value < 28:
        return 2
    return 1


def cosmic_frame_3(tick):
    value = tick % 47
    if value < 13:
        return 0
    if value < 14:
        return 1
    if value < 24:
        return 0
    if value < 25:
        return 3
    if value < 30:
        return 0
    if value < 31:
        return 2
    if value < 46:
        return 0
    return 4


def cosmic_frame_4(tick):
    value = tick % 37
    return 0 if value < 34 else value - 33


def cosmic_frame_5(tick):
    value = tick % 39
    if value < 18:
        return 0
    if value < 19:
        return 1
    if value < 23:
        return 0
    if value < 24:
        return 3
    if value < 38:
        return 0
    return 2


def cosmic_frame_8(tick):
    value = tick % 84
    if value < 1:
        return 1
    if value < 2:
        return 2
    if value < 3:
        return 3
    if value < 4:
        return 2
    if value < 5:
        return 3
    if value < 6:
        return 2
    if value < 7:
        return 1
    if value < 29:
        return 0
    if value < 30:
        return 4
    if value < 31:
        return 5
    if value < 32:
        return 6
    if value < 33:
        return 5
    if value < 34:
        return 6
    if value < 35:
        return 5
    if value < 36:
        return 4
    if value < 67:
        return 0
    if value < 68:
        return 1
    if value < 69:
        return 2
    if value < 70:
        return 3
    if value < 71:
        return 2
    if value < 72:
        return 1
    return 0


class Texture:
    def __init__(self, path, magick):
        dimensions = subprocess.check_output(
            [magick, "identify", "-format", "%w %h", str(path)],
            text=True,
        ).split()
        self.width, self.height = map(int, dimensions)
        self.data = subprocess.check_output(
            [magick, str(path), "-depth", "8", "rgba:-"]
        )

    def sample(self, u, v):
        x = min(self.width - 1, max(0, int(clamp(u) * self.width)))
        y = min(self.height - 1, max(0, int(clamp(v) * self.height)))
        offset = (y * self.width + x) * 4
        return tuple(channel / 255.0 for channel in self.data[offset:offset + 4])


def sample_cosmic(textures, symbol, u, v, tick):
    if symbol == 0:
        frame, count = cosmic_frame_0(tick), 4
    elif symbol == 1:
        frame, count = cosmic_frame_1(tick), 4
    elif symbol == 2:
        frame, count = cosmic_frame_2(tick), 5
    elif symbol == 3:
        frame, count = cosmic_frame_3(tick), 5
    elif symbol == 4:
        frame, count = cosmic_frame_4(tick), 4
    elif symbol == 5:
        frame, count = cosmic_frame_5(tick), 4
    elif symbol == 6:
        frame, count = tick % 6, 6
    elif symbol == 7:
        frame, count = (tick % 8) // 2, 4
    elif symbol == 8:
        frame, count = cosmic_frame_8(tick), 7
    else:
        frame, count = (tick % 6) // 2, 3
    return textures[symbol].sample(u, (v + frame) / count)


def rotate_ray(direction, axis, angle):
    x, y, z = axis
    sine = math.sin(angle)
    cosine = math.cos(angle)
    one_minus_cosine = 1.0 - cosine
    columns = (
        (
            one_minus_cosine * x * x + cosine,
            one_minus_cosine * x * y - z * sine,
            one_minus_cosine * z * x + y * sine,
        ),
        (
            one_minus_cosine * x * y + z * sine,
            one_minus_cosine * y * y + cosine,
            one_minus_cosine * y * z - x * sine,
        ),
        (
            one_minus_cosine * z * x - y * sine,
            one_minus_cosine * y * z + x * sine,
            one_minus_cosine * z * z + cosine,
        ),
    )
    return tuple(dot(direction, column) for column in columns)


def render_pixel(u, v, tick, layer_0, layer_1, blade_mask, cosmic_textures):
    frame_0 = sword_frame(tick)
    frame_1 = (tick % 56) // 2
    body = layer_0.sample(u, (v + frame_0) / 9.0)
    edge = layer_1.sample(u, (v + frame_1) / 28.0)
    mask = blade_mask.sample(u, (v + frame_0) / 9.0)[0]
    base = (
        body[0] * (1.0 - edge[3]) + edge[0] * edge[3],
        body[1] * (1.0 - edge[3]) + edge[1] * edge[3],
        body[2] * (1.0 - edge[3]) + edge[2] * edge[3],
        edge[3] + body[3] * (1.0 - edge[3]),
    )

    color_pulse = (tick % 400) / 400.0
    cosmic = [
        0.1,
        math.sin(color_pulse * PI * 2.0) * 0.075 + 0.225,
        math.cos(color_pulse * PI * 2.0) * 0.05 + 0.3,
        1.0,
    ]
    direction = normalize((1.0 - u * 2.0, v * 2.0 - 1.0, 2.0))

    for index in range(16):
        multiplier = 16 - index
        j = index + 7
        random_1 = float(j * j * 4321 + j * 8) * 2.0
        k = j + 1
        random_2 = float(k * k * k * 239 + k * 37) * 3.6
        random_3 = random_1 * 347.4 + random_2 * 63.4
        axis = normalize(
            (math.sin(random_1), math.sin(random_2), math.cos(random_3))
        )
        ray = rotate_ray(direction, axis, random_3 % (2.0 * PI))
        raw_u = 0.5 + math.atan2(ray[2], ray[0]) / (2.0 * PI)
        raw_v = 0.5 + math.asin(clamp(ray[1], -1.0, 1.0)) / PI
        scale = multiplier * 0.5 + 2.75
        projected_u = raw_u * scale * EXTERNAL_SCALE
        projected_v = (
            raw_v + tick * 0.0002 / EXTERNAL_SCALE
        ) * scale * 0.6 * EXTERNAL_SCALE

        tile_u = int(math.floor(projected_u * 16.0) % 16)
        tile_v = int(math.floor(projected_v * 16.0) % 16)
        symbol = int(rand2d(tile_u, tile_v + index * 10) * 101.0)
        rotation = int(
            (
                math.pow(float(tile_u), float(tile_v))
                + float(tile_u + 3 + tile_v * index)
            )
            % 8.0
        )
        flip = rotation >= 4
        if flip:
            rotation -= 4

        if symbol >= 10:
            continue

        local_u = clamp((projected_u % 1.0) * 16.0 - tile_u)
        local_v = clamp((projected_v % 1.0) * 16.0 - tile_v)
        if flip:
            local_u = 1.0 - local_u

        if rotation == 1:
            oriented_u, oriented_v = 1.0 - local_v, local_u
        elif rotation == 2:
            oriented_u, oriented_v = 1.0 - local_u, 1.0 - local_v
        elif rotation == 3:
            oriented_u, oriented_v = local_v, 1.0 - local_u
        else:
            oriented_u, oriented_v = local_u, local_v

        texture_color = sample_cosmic(
            cosmic_textures,
            symbol,
            oriented_u,
            oriented_v,
            tick,
        )
        alpha = (
            texture_color[0]
            * (0.5 + 1.0 / multiplier)
            * (1.0 - smoothstep(0.15, 0.48, abs(raw_v - 0.5)))
        )
        red = (random_1 % 29.0) / 29.0 * 0.3 + 0.4
        green = (random_2 % 35.0) / 35.0 * 0.4 + 0.6
        blue = (random_1 % 17.0) / 17.0 * 0.3 + 0.7
        cosmic[0] += red * alpha
        cosmic[1] += green * alpha
        cosmic[2] += blue * alpha
        cosmic[3] += alpha

    shade = GUI_LIGHT_LEVEL * LIGHT_MIX + 1.0 - LIGHT_MIX
    cosmic[0] = clamp(cosmic[0] * shade)
    cosmic[1] = clamp(cosmic[1] * shade)
    cosmic[2] = clamp(cosmic[2] * shade)
    cosmic[3] = clamp(cosmic[3] * mask)
    inverse_alpha = 1.0 - cosmic[3]
    return (
        cosmic[0] * cosmic[3] + base[0] * inverse_alpha,
        cosmic[1] * cosmic[3] + base[1] * inverse_alpha,
        cosmic[2] * cosmic[3] + base[2] * inverse_alpha,
        cosmic[3] + base[3] * inverse_alpha,
    )


def render(args):
    magick = shutil.which("magick")
    if magick is None:
        raise RuntimeError("ImageMagick is required.")

    assets = args.assets.resolve()
    layer_0 = Texture(assets / "infinity_sword_layer_0.png", magick)
    layer_1 = Texture(assets / "infinity_sword_layer_1.png", magick)
    blade_mask = Texture(assets / "infinity_sword_mask.png", magick)
    cosmic_textures = [
        Texture(assets / f"cosmic_{index}.png", magick)
        for index in range(10)
    ]

    pixels = bytearray()
    for y in range(args.size):
        v = (y + 0.5) / args.size
        for x in range(args.size):
            u = (x + 0.5) / args.size
            color = render_pixel(
                u,
                v,
                args.tick,
                layer_0,
                layer_1,
                blade_mask,
                cosmic_textures,
            )
            pixels.extend(
                max(0, min(255, round(channel * 255.0)))
                for channel in color
            )

    args.output.parent.mkdir(parents=True, exist_ok=True)
    subprocess.run(
        [
            magick,
            "-size",
            f"{args.size}x{args.size}",
            "-depth",
            "8",
            "rgba:-",
            "-strip",
            str(args.output),
        ],
        input=bytes(pixels),
        check=True,
    )


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--assets", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--size", type=int, default=160)
    parser.add_argument("--tick", type=int, default=0)
    render(parser.parse_args())


if __name__ == "__main__":
    main()
