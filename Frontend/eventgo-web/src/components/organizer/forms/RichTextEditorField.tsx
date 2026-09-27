import React from 'react';
import { useEditor, EditorContent } from '@tiptap/react';
import StarterKit from '@tiptap/starter-kit';
import { TextStyle } from '@tiptap/extension-text-style';
import { Color } from '@tiptap/extension-color';
import TextAlign from '@tiptap/extension-text-align';
import Link from '@tiptap/extension-link';
import Image from '@tiptap/extension-image';
import { Bold, Italic, Underline, AlignLeft, AlignCenter, AlignRight, AlignJustify, List, ListOrdered, ImageIcon, Video } from 'lucide-react';

interface RichTextEditorFieldProps {
    value: string;
    onChange: (value: string) => void;
    error?: string;
}

const MenuBar = ({ editor }: { editor: any }) => {
    if (!editor) {
        return null;
    }

    return (
        <div className="flex flex-wrap items-center gap-1 border-b border-gray-300 bg-gray-50 p-2 text-black rounded-t-md">
            <select
                className="rounded border border-gray-300 px-2 py-1 text-sm bg-white cursor-pointer"
                onChange={(e) => {
                    if (e.target.value === 'p') editor.chain().focus().setParagraph().run();
                    else if (e.target.value === 'h1') editor.chain().focus().toggleHeading({ level: 1 }).run();
                    else if (e.target.value === 'h2') editor.chain().focus().toggleHeading({ level: 2 }).run();
                    else if (e.target.value === 'h3') editor.chain().focus().toggleHeading({ level: 3 }).run();
                }}
                defaultValue="p"
            >
                <option value="p">Paragraph</option>
                <option value="h1">Heading 1</option>
                <option value="h2">Heading 2</option>
                <option value="h3">Heading 3</option>
            </select>

            <div className="mx-1 h-5 w-px bg-gray-300" />

            {/* Colors */}
            <button type="button" onClick={() => editor.chain().focus().setColor('#FFFFFF').run()} className="h-5 w-5 rounded-full bg-white border border-gray-300 cursor-pointer" title="Trắng" />
            <button type="button" onClick={() => editor.chain().focus().setColor('#16A34A').run()} className="h-5 w-5 rounded-full bg-green-600 border border-transparent cursor-pointer" title="Xanh lá" />
            <button type="button" onClick={() => editor.chain().focus().setColor('#EF4444').run()} className="h-5 w-5 rounded-full bg-red-500 border border-transparent cursor-pointer" title="Đỏ" />
            <button type="button" onClick={() => editor.chain().focus().setColor('#EAB308').run()} className="h-5 w-5 rounded-full bg-yellow-500 border border-transparent cursor-pointer" title="Vàng" />

            <div className="mx-1 h-5 w-px bg-gray-300" />

            {/* Formats */}
            <button type="button" onClick={() => editor.chain().focus().toggleBold().run()} className={`p-1.5 rounded hover:bg-gray-200 cursor-pointer ${editor.isActive('bold') ? 'bg-gray-200' : ''}`}>
                <Bold className="h-4 w-4" />
            </button>
            <button type="button" onClick={() => editor.chain().focus().toggleItalic().run()} className={`p-1.5 rounded hover:bg-gray-200 cursor-pointer ${editor.isActive('italic') ? 'bg-gray-200' : ''}`}>
                <Italic className="h-4 w-4" />
            </button>

            <div className="mx-1 h-5 w-px bg-gray-300" />

            {/* Align */}
            <button type="button" onClick={() => editor.chain().focus().setTextAlign('left').run()} className={`p-1.5 rounded hover:bg-gray-200 cursor-pointer ${editor.isActive({ textAlign: 'left' }) ? 'bg-gray-200' : ''}`}>
                <AlignLeft className="h-4 w-4" />
            </button>
            <button type="button" onClick={() => editor.chain().focus().setTextAlign('center').run()} className={`p-1.5 rounded hover:bg-gray-200 cursor-pointer ${editor.isActive({ textAlign: 'center' }) ? 'bg-gray-200' : ''}`}>
                <AlignCenter className="h-4 w-4" />
            </button>
            <button type="button" onClick={() => editor.chain().focus().setTextAlign('right').run()} className={`p-1.5 rounded hover:bg-gray-200 cursor-pointer ${editor.isActive({ textAlign: 'right' }) ? 'bg-gray-200' : ''}`}>
                <AlignRight className="h-4 w-4" />
            </button>
            <button type="button" onClick={() => editor.chain().focus().setTextAlign('justify').run()} className={`p-1.5 rounded hover:bg-gray-200 cursor-pointer ${editor.isActive({ textAlign: 'justify' }) ? 'bg-gray-200' : ''}`}>
                <AlignJustify className="h-4 w-4" />
            </button>

            <div className="mx-1 h-5 w-px bg-gray-300" />

            {/* Lists */}
            <button type="button" onClick={() => editor.chain().focus().toggleBulletList().run()} className={`p-1.5 rounded hover:bg-gray-200 cursor-pointer ${editor.isActive('bulletList') ? 'bg-gray-200' : ''}`}>
                <List className="h-4 w-4" />
            </button>
            <button type="button" onClick={() => editor.chain().focus().toggleOrderedList().run()} className={`p-1.5 rounded hover:bg-gray-200 cursor-pointer ${editor.isActive('orderedList') ? 'bg-gray-200' : ''}`}>
                <ListOrdered className="h-4 w-4" />
            </button>

            <div className="mx-1 h-5 w-px bg-gray-300" />

            {/* Media placeholder */}
            <button type="button" className="p-1.5 rounded hover:bg-gray-200 cursor-pointer title-Image">
                <ImageIcon className="h-4 w-4" />
            </button>
            <button type="button" className="p-1.5 rounded hover:bg-gray-200 cursor-pointer title-Video">
                <Video className="h-4 w-4" />
            </button>
        </div>
    );
};

export const RichTextEditorField: React.FC<RichTextEditorFieldProps> = ({ value, onChange, error }) => {
    const editor = useEditor({
        extensions: [
            StarterKit,
            TextStyle,
            Color,
            TextAlign.configure({
                types: ['heading', 'paragraph'],
            }),
            Link,
            Image,
        ],
        content: value,
        onUpdate: ({ editor }) => {
            onChange(editor.getHTML());
        },
        editorProps: {
            attributes: {
                class: 'prose prose-sm xl:prose-base focus:outline-none min-h-[300px] max-h-[500px] overflow-y-auto p-4 bg-white text-black rounded-b-md',
            },
        },
    });

    return (
        <div className="flex w-full flex-col gap-2">
            <label className="text-sm font-medium text-white">
                Thông tin sự kiện <span className="text-green-500">*</span>
            </label>
            <div className={`rounded-md border ${error ? 'border-red-500' : 'border-gray-300'} bg-white overflow-hidden`}>
                <MenuBar editor={editor} />
                <EditorContent editor={editor} />
            </div>
            {error && <p className="text-xs text-red-500">{error}</p>}
        </div>
    );
};
