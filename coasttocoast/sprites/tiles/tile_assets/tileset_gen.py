#!/usr/bin/env python3
"""
Tileset generator with curved corners - creates a complete tileset from a single image
Usage: python tileset_generator.py input_image.jpg output_tileset.png --radius 20
"""

import numpy as np
from PIL import Image, ImageDraw
import argparse
import sys

def create_corner_mask(width, height, radius, corners_to_round):
    """
    Create a mask with specified corners rounded
    corners_to_round: list of strings like ['top_left', 'top_right', 'bottom_left', 'bottom_right']
    """
    # Create a white mask (fully opaque)
    mask = Image.new('L', (width, height), 255)
    draw = ImageDraw.Draw(mask)
    
    # Round each specified corner
    for corner in corners_to_round:
        if corner == 'top_left':
            # Black out corner area
            draw.rectangle([0, 0, radius, radius], fill=0)
            # Draw white circle to create curve
            draw.ellipse([0, 0, radius * 2, radius * 2], fill=255)
            
        elif corner == 'top_right':
            # Black out corner area
            draw.rectangle([width - radius, 0, width, radius], fill=0)
            # Draw white circle to create curve
            draw.ellipse([width - radius * 2, 0, width, radius * 2], fill=255)
            
        elif corner == 'bottom_left':
            # Black out corner area
            draw.rectangle([0, height - radius, radius, height], fill=0)
            # Draw white circle to create curve
            draw.ellipse([0, height - radius * 2, radius * 2, height], fill=255)
            
        elif corner == 'bottom_right':
            # Black out corner area
            draw.rectangle([width - radius, height - radius, width, height], fill=0)
            # Draw white circle to create curve
            draw.ellipse([width - radius * 2, height - radius * 2, width, height], fill=255)
    
    return mask

def create_tileset(image_path, output_path, radius):
    """
    Create a complete tileset from a single image
    """
    try:
        # Open the base image
        base_img = Image.open(image_path)
        
        # Convert to RGBA if not already
        if base_img.mode != 'RGBA':
            base_img = base_img.convert('RGBA')
        
        width, height = base_img.size
        
        # Validate radius
        max_radius = min(width, height) // 2
        if radius > max_radius:
            print(f"Warning: Radius {radius} is too large for image size {width}x{height}")
            print(f"Using maximum radius: {max_radius}")
            radius = max_radius
        
        # Create the tileset canvas (W*8 x H*2)
        tileset_width = width * 8
        tileset_height = height * 2
        tileset = Image.new('RGBA', (tileset_width, tileset_height), (0, 0, 0, 0))
        
        # Define tile configurations
        # Format: (row, col): [corners_to_round]
        tile_configs = {
            (1, 1): ['top_left', 'top_right', 'bottom_left', 'bottom_right'],  # all corners
            (1, 2): ['top_left', 'bottom_left'],                              # left side
            (1, 3): ['top_right', 'bottom_right'],                           # right side
            (1, 4): ['top_left', 'top_right'],                               # top side
            (1, 5): ['bottom_left', 'bottom_right'],                         # bottom side
            (1, 6): ['top_right'],                                           # top right only
            (1, 7): ['top_left'],                                            # top left only
            (1, 8): None,                                                    # fully transparent
            (2, 1): ['bottom_right'],                                        # bottom right only
            (2, 2): ['bottom_left'],                                         # bottom left only
            (2, 3): [],                                                      # no corners (base image)
            # All others are transparent (None)
        }
        
        # Generate each tile
        for row in range(1, 3):  # rows 1-2
            for col in range(1, 9):  # cols 1-8
                tile_key = (row, col)
                
                # Calculate position in tileset
                x = (col - 1) * width
                y = (row - 1) * height
                
                if tile_key in tile_configs:
                    corners = tile_configs[tile_key]
                    
                    if corners is None:
                        # Fully transparent tile - skip (leave as transparent)
                        continue
                    else:
                        # Create tile with specified corners rounded
                        tile_img = base_img.copy()
                        
                        if corners:  # If there are corners to round
                            mask = create_corner_mask(width, height, radius, corners)
                            tile_img.putalpha(mask)
                        
                        # Paste tile into tileset
                        tileset.paste(tile_img, (x, y))
                
                # All other positions remain transparent
        
        # Save the tileset
        tileset.save(output_path, 'PNG')
        print(f"Tileset generated! Saved to: {output_path}")
        print(f"Tileset size: {tileset_width}x{tileset_height}")
        print(f"Individual tile size: {width}x{height}")
        print(f"Corner radius: {radius}px")
        print(f"Total tiles: 16 (11 with content, 5 transparent)")
        
    except FileNotFoundError:
        print(f"Error: Could not find image file: {image_path}")
        sys.exit(1)
    except Exception as e:
        print(f"Error processing image: {e}")
        sys.exit(1)

def main():
    parser = argparse.ArgumentParser(
        description='Generate a complete tileset with curved corners from a single image',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog='''
Tileset Layout (8x2 tiles):
Row 1: All corners | Left side | Right side | Top side | Bottom side | Top-right | Top-left | Unused
Row 2: Bottom-right | Bottom-left | Base image | Transparent | Transparent | Transparent | Transparent | Transparent

Examples:
  python tileset_generator.py grass.png grass_tileset.png --radius 25
  python tileset_generator.py stone.jpg stone_tiles.png -r 15
        '''
    )
    
    parser.add_argument('input', help='Input image file path')
    parser.add_argument('output', help='Output tileset file path (PNG recommended)')
    parser.add_argument('-r', '--radius', type=int, default=20, 
                       help='Corner radius in pixels (default: 20)')
    
    args = parser.parse_args()
    
    # Validate radius
    if args.radius <= 0:
        print("Error: Radius must be greater than 0")
        sys.exit(1)
    
    create_tileset(args.input, args.output, args.radius)

if __name__ == '__main__':
    main()